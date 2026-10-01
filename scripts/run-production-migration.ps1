$ErrorActionPreference = 'Stop'
$stack = if ($env:AWS_STACK_NAME) { $env:AWS_STACK_NAME } else { 'proofpath-production' }
$outputs = aws cloudformation describe-stacks --stack-name $stack --query 'Stacks[0].Outputs' | ConvertFrom-Json
function Output([string]$key) { ($outputs | Where-Object OutputKey -eq $key).OutputValue }
$cluster = Output 'ClusterName'
$task = Output 'ApiTaskDefinition'
if (-not $cluster -or -not $task) { throw 'The production stack does not expose the migration task settings.' }
if (-not $env:PROOFPATH_MIGRATION_NETWORK) { throw 'Set PROOFPATH_MIGRATION_NETWORK to the reviewed ECS awsvpc configuration.' }
$override = '{"containerOverrides":[{"name":"api","command":["dotnet","ProofPath.Api.dll","--migrate"]}]}'
$result = aws ecs run-task --cluster $cluster --task-definition $task --launch-type FARGATE --network-configuration $env:PROOFPATH_MIGRATION_NETWORK --overrides $override | ConvertFrom-Json
$arn = $result.tasks[0].taskArn
if (-not $arn) { throw 'AWS did not start the migration task.' }
aws ecs wait tasks-stopped --cluster $cluster --tasks $arn
$exitCode = aws ecs describe-tasks --cluster $cluster --tasks $arn --query 'tasks[0].containers[?name==`api`].exitCode | [0]' --output text
if ($exitCode -ne '0') { throw "Migration task failed with exit code $exitCode." }
