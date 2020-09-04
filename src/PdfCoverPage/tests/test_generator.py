import pytest
from moto import mock_s3

from generator import *


@pytest.fixture(scope='function')
def aws_credentials():
    """Mocked AWS Credentials for moto."""
    os.environ["AWS_ACCESS_KEY_ID"] = "testing"
    os.environ["AWS_SECRET_ACCESS_KEY"] = "testing"
    os.environ["AWS_SECURITY_TOKEN"] = "testing"
    os.environ["AWS_SESSION_TOKEN"] = "testing"
    os.environ["MANIFEST_BUCKET"] = "test_bucket_pdf_gen"
    os.environ["AWS_REGION"] = "us-east-1"  # required for moto


def test_returns_400_if_no_querystring_values():
    result = lambda_handler({}, [])

    expected = {
        "isBase64Encoded": False,
        "statusCode": 400,
        "statusDescription": "400 - Bad Request",
        "body": "No identifier found",
        "headers": {"Content-Type": "text/plain"}
    }

    assert result == expected


def test_returns_400_if_no_identifier_querystring_value():
    result = lambda_handler({"queryStringParameters": {"foo": "bar"}}, [])

    expected = {
        "isBase64Encoded": False,
        "statusCode": 400,
        "statusDescription": "400 - Bad Request",
        "body": "No identifier found",
        "headers": {"Content-Type": "text/plain"}
    }

    assert result == expected


@pytest.fixture(scope='function')
def s3(aws_credentials):
    with mock_s3():
        yield boto3.client('s3', region_name=os.environ["AWS_REGION"])


def test_throws_404_if_error_other_than_nosuchkey(s3):
    with pytest.raises(ClientError):
        result = lambda_handler({"queryStringParameters": {"identifier": "b18310916"}}, [])

        expected = {
            "isBase64Encoded": False,
            "statusCode": 404,
            "statusDescription": "404 - Not Found",
            "body": "Manifest for identifier not found",
            "headers": {"Content-Type": "text/plain"}
        }

        assert result == expected


def test_returns_404_if_manifest_not_found(s3):
    s3.create_bucket(Bucket=os.environ["MANIFEST_BUCKET"])

    result = lambda_handler({"queryStringParameters": {"identifier": "b18310916"}}, [])

    expected = {
        "isBase64Encoded": False,
        "statusCode": 404,
        "statusDescription": "404 - Not Found",
        "body": "Manifest for identifier not found",
        "headers": {"Content-Type": "text/plain"}
    }

    assert result == expected
