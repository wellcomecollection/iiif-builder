from generator import *


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
