import json
import os
from botocore.exceptions import ClientError
from http import HTTPStatus

import boto3


def lambda_handler(event, context):
    """
    Event handler for lambda call
    :param event: Contains details of event see https://docs.aws.amazon.com/lambda/latest/dg/lambda-services.html
    :param context: Contains context of lambda request. see https://docs.aws.amazon.com/lambda/latest/dg/python-context.html
    :return: Generated PDF as a stream
    """
    identifier = event.get("queryStringParameters", {}).get("identifier", "")
    if not identifier:
        return generate_response(HTTPStatus.BAD_REQUEST, "No identifier found", {"Content-Type": "text/plain"})

    return generate_pdf(identifier)


def generate_pdf(identifier: str):
    # make request to get manifest from s3
    manifest = get_json_object(identifier)

    if not manifest:
        return generate_response(HTTPStatus.NOT_FOUND, "Manifest for identifier not found",
                                 {"Content-Type": "text/plain"})

    # generate PDF and return base64 encoded
    print(f"generating PDF cover-page for {identifier}")


def get_json_object(identifier: str) -> dict:
    """
    Gets specified json-containing key as a dict
    :param identifier: manifest to get
    :return: Key as dictionary
    """
    region = os.environ.get("AWS_REGION", "eu-west-1")
    manifest_bucket = os.environ.get("MANIFEST_BUCKET", "wellcomecollection-stage-iiif-presentation")
    key_prefix = os.environ.get("KEY_PREFIX", "v3")

    try:
        client = boto3.client("s3", region)
        response = client.get_object(Bucket=manifest_bucket, Key=f"{key_prefix}/{identifier}")
        manifest = response.get("Body", "").read().decode()
        return json.loads(manifest)
    except ClientError as e:
        if e.response["Error"]["Code"] == "NoSuchKey":
            return {}

        raise


def generate_response(http_status: HTTPStatus, body: str, headers: dict, is_base64: bool = False) -> dict:
    """
    Generate response object that will be correctly understood by ELB
    :param http_status: Status code for response
    :param body: Body of response
    :param headers: Any headers to add to response
    :param is_base64: true if binary, else false
    :return: dictionary containing response object understood by ELB
    """
    # Sample response -
    # {
    #     "isBase64Encoded": false,
    #     "statusCode": 200,
    #     "statusDescription": "200 OK",
    #     "headers": {
    #         "Set-cookie": "cookies",
    #         "Content-Type": "application/json"
    #     },
    #     "body": "Hello from Lambda (optional)"
    # }
    # TODO - allow content-type to be specified
    response = {
        "isBase64Encoded": is_base64,
        "statusCode": http_status.value,
        "statusDescription": f'{http_status.value} - {http_status.phrase}',
        "body": body,
        "headers": headers
    }
    return response


if __name__ == '__main__':
    event = []
    with open('event.json') as event_json:
        event = json.load(event_json)

    result = lambda_handler(event, [])
    print(result)
