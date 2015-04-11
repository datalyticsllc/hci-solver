#! /bin/bash

# Get the instance name
INSTANCE_ID=$(curl http://metadata/computeMetadata/v1beta1/instance/attributes/serverId)

# Now shut down the server
gcutil --project="smart-amplifier-286" deleteinstance $INSTANCE_ID --force



