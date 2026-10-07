"""Exercise the deployed ClamAV image and health command before releasing."""
import json
import shlex
import subprocess
import time
from pathlib import Path
import yaml

root = yaml.compose(Path("infra/production.yml").read_text())
def field(node, name):
    return next(value for key, value in node.value if key.value == name)
resources = field(root, "Resources")
containers = field(field(field(resources, "ApiTask"), "Properties"), "ContainerDefinitions")
clamav = next(c for c in containers.value if field(c, "Name").value == "clamav")
image = field(field(field(root, "Parameters"), "ClamAvImage"), "Default").value
command = [v.value for v in field(field(clamav, "HealthCheck"), "Command").value]
health = shlex.join(command[1:]) if command[0] == "CMD" else command[1]
container = subprocess.check_output([
    "docker", "run", "--detach", "--health-cmd", health,
    "--health-interval", "5s", "--health-timeout", "10s",
    "--health-start-period", "180s", "--health-retries", "5", image
], text=True).strip()
try:
    for _ in range(72):
        state = json.loads(subprocess.check_output(["docker", "inspect", container], text=True))[0]["State"]
        if state.get("Health", {}).get("Status") == "healthy":
            print("Deployed ClamAV image passed its health command.")
            break
        if state["Status"] != "running":
            raise RuntimeError("ClamAV exited during startup")
        time.sleep(5)
    else:
        print(json.dumps(state.get("Health", {}), indent=2))
        raise RuntimeError("ClamAV did not become healthy within 6 minutes")
finally:
    subprocess.run(["docker", "rm", "--force", container], check=True)
