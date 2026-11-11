from datetime import datetime, timezone
from pathlib import Path
import os
import sys
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import yaml


TEMPLATE = Path(__file__).resolve().parents[1] / "infra" / "staging-rotation.yml"
ACCOUNT = "123456789012"
CLUSTER = f"arn:aws:ecs:us-east-2:{ACCOUNT}:cluster/proofpath-staging"
API = f"arn:aws:ecs:us-east-2:{ACCOUNT}:service/proofpath-staging/proofpath-staging-app-ApiService-Test"
WORKER = f"arn:aws:ecs:us-east-2:{ACCOUNT}:service/proofpath-staging/proofpath-staging-app-WorkerService-Test"
SECRET = f"arn:aws:secretsmanager:us-east-2:{ACCOUNT}:secret:rds!db-synthetic"
ROTATED = "2026-11-11T17:00:00Z"


def service(arn, minute=0, rollout="COMPLETED", count=1):
    return {
        "serviceArn": arn,
        "status": "ACTIVE",
        "desiredCount": count,
        "deployments": [{
            "status": "PRIMARY",
            "rolloutState": rollout,
            "createdAt": datetime(2026, 11, 11, 16 if minute == 0 else 17, minute, tzinfo=timezone.utc),
        }],
    }


class ClientError(Exception):
    def __init__(self, code):
        self.response = {"Error": {"Code": code}}


class RotationTests(unittest.TestCase):
    def setUp(self):
        self.ecs = Mock()
        self.leases = Mock()
        self.ecs.describe_services.return_value = {"services": [service(API), service(WORKER)], "failures": []}
        boto3 = ModuleType("boto3")
        boto3.client = Mock(side_effect=lambda name, **kwargs: {"ecs": self.ecs, "dynamodb": self.leases}[name])
        botocore = ModuleType("botocore")
        config = ModuleType("botocore.config")
        config.Config = Mock()
        exceptions = ModuleType("botocore.exceptions")
        exceptions.ClientError = ClientError
        self.enterContext(patch.dict(sys.modules, {
            "boto3": boto3, "botocore": botocore, "botocore.config": config, "botocore.exceptions": exceptions,
        }))
        self.enterContext(patch.dict(os.environ, {
            "EXPECTED_ACCOUNT": ACCOUNT, "EXPECTED_SECRET_ARN": SECRET, "ECS_CLUSTER": CLUSTER,
            "API_SERVICE": API, "WORKER_SERVICE": WORKER, "LEASE_TABLE": "rotation-lease-test",
        }))
        template = yaml.load(TEMPLATE.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
        code = template["Resources"]["RotationRefresh"]["Properties"]["Code"]["ZipFile"]
        namespace = {}
        exec(compile(code, str(TEMPLATE), "exec"), namespace)
        self.handler = lambda event: namespace["handler"](event, SimpleNamespace(aws_request_id="test-request"))
        self.event = {
            "source": "aws.secretsmanager", "detail-type": "Secret Label Updated",
            "region": "us-east-2", "account": ACCOUNT, "time": ROTATED,
            "resources": [SECRET], "detail": {"labelUpdated": "AWSCURRENT"},
        }

    def test_refreshes_both_services_without_changing_the_task_definition(self):
        self.assertEqual(self.handler(self.event)["refreshCount"], 2)
        self.ecs.describe_services.assert_called_once_with(cluster=CLUSTER, services=[API, WORKER])
        self.assertEqual(
            [call.kwargs for call in self.ecs.update_service.call_args_list],
            [{"cluster": CLUSTER, "service": arn, "forceNewDeployment": True} for arn in [API, WORKER]],
        )
        self.leases.put_item.assert_called_once()
        self.leases.delete_item.assert_called_once()
        self.assertEqual(self.leases.delete_item.call_args.kwargs["ExpressionAttributeValues"], {":owner": {"S": "test-request"}})

    def test_unrelated_account_region_secret_and_label_do_not_touch_ecs(self):
        for override in [
            {"account": "000000000000"}, {"region": "us-east-1"}, {"resources": []},
            {"detail": {"labelUpdated": "AWSPREVIOUS"}}, {"source": "aws.cloudwatch"},
            {"detail-type": "AWS API Call via CloudTrail"},
        ]:
            with self.subTest(override=override):
                self.assertEqual(self.handler(self.event | override)["status"], "ignored")
        self.ecs.describe_services.assert_not_called()
        self.ecs.update_service.assert_not_called()
        self.leases.put_item.assert_not_called()

    def test_concurrent_delivery_cannot_refresh_or_release_another_lease(self):
        self.leases.put_item.side_effect = ClientError("ConditionalCheckFailedException")
        with self.assertRaisesRegex(RuntimeError, "concurrent staging refresh"):
            self.handler(self.event)
        self.ecs.describe_services.assert_not_called()
        self.leases.delete_item.assert_not_called()

    def test_retry_only_refreshes_service_that_has_not_already_rolled(self):
        self.ecs.describe_services.return_value["services"] = [service(API, minute=1, rollout="IN_PROGRESS"), service(WORKER)]
        self.assertEqual(self.handler(self.event)["refreshCount"], 1)
        self.ecs.update_service.assert_called_once_with(cluster=CLUSTER, service=WORKER, forceNewDeployment=True)

    def test_waits_for_existing_older_rollout_before_refreshing_either_service(self):
        self.ecs.describe_services.return_value["services"] = [service(API), service(WORKER, rollout="IN_PROGRESS")]
        with self.assertRaisesRegex(RuntimeError, "existing staging rollout"):
            self.handler(self.event)
        self.ecs.update_service.assert_not_called()
        self.leases.delete_item.assert_called_once()

    def test_missing_service_and_failed_rollout_are_not_mutated(self):
        for services in [[service(API)], [service(API), service(WORKER, minute=1, rollout="FAILED")]]:
            with self.subTest(services=services):
                self.ecs.describe_services.return_value["services"] = services
                with self.assertRaises(RuntimeError):
                    self.handler(self.event)
        self.ecs.update_service.assert_not_called()

    def test_does_not_resume_a_service_scaled_to_zero(self):
        self.ecs.describe_services.return_value["services"] = [service(API, minute=1), service(WORKER, count=0)]
        self.assertEqual(self.handler(self.event)["refreshCount"], 0)
        self.ecs.update_service.assert_not_called()


if __name__ == "__main__":
    unittest.main()
