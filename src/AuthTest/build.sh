#! /bin/bash

if [ -z "$1" ]
then
  echo "No account number provided! Aborting."
  exit 1
fi

DOCKER_IMAGE="$1.dkr.ecr.eu-west-1.amazonaws.com/iiif-builder-auth"

echo "Logging into ECR..."
aws ecr get-login-password --region eu-west-1 --profile wcdev | docker login --username AWS --password-stdin $1.dkr.ecr.eu-west-1.amazonaws.com

echo "Building docker image..."
docker build -t ${DOCKER_IMAGE}:test .

echo "Tagging docker image..."
docker tag ${DOCKER_IMAGE}:test ${DOCKER_IMAGE}:`git rev-parse HEAD`

echo "Pushing docker image..."
docker push ${DOCKER_IMAGE}:test
docker push ${DOCKER_IMAGE}:`git rev-parse HEAD`

# echo "Restarting ECS service.."
aws ecs update-service --force-new-deployment --cluster iiif-builder-stage --service auth-test-stage --region eu-west-1 --profile wcdev
aws ecs wait services-stable --cluster iiif-builder-stage --service auth-test-stage --region eu-west-1 --profile wcdev
