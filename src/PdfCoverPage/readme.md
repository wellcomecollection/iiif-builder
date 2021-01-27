# PDF Cover Page

PDF Cover Page generator. This is run as an AWS Lambda Python function and called through ELB.

The function will take an `?identifier=` query string parameter and generate the PDF cover-page for that identifier.

All data for the cover-page will be taken from the Manifest, stored in S3. If the manifest does not exist the call will fail.

Uses [reportLab](https://www.reportlab.com/) library for PDF generation.

Uses open source [noto-fonts](https://github.com/googlefonts/noto-fonts) as build in fonts for reportLab do not render all required characters correctly. 

## Running Locally

The function can be run normal via:

```bash 
python generator.py
```

This will read the contents of `event.json` to simulate the event data that will be sent to the lambda function via ELB. See [AWS Docs](https://docs.aws.amazon.com/lambda/latest/dg/services-alb.html) for more details on the shape of the event object.

If the response from the handler is base64 encoded then this is saved to disk as `{identifier}.pdf`.

Edit the `queryStringParameters` property to simulate passing an identifier.

```json
"queryStringParameters": {
  "identifier": "b24967646"
},
```

## Return Values

Aside from 500 error there are 3 known response payloads, these are converted from JSON to HTTP response by ELB integration:

### Bad Request

Returned if `?identifier` not found.

```json
{
    "isBase64Encoded": false,
    "statusCode": 400,
    "statusDescription": "400 - Bad Request",
    "headers": {
        "Content-Type": "text/plan"
    },
    "body": "No identifier found"
}
```

### Not Found

Returned if `?identifier` passed but manifest doesn't exist in S3.

```json
{
    "isBase64Encoded": false,
    "statusCode": 404,
    "statusDescription": "404 - Not Found",
    "headers": {
        "Content-Type": "text/plan"
    },
    "body": "Manifest for identifier not found"
}
```

### Success

Returns base64 encoded PDF in body.

```json
{
    "isBase64Encoded": true,
    "statusCode": 200,
    "statusDescription": "200 - OK",
    "headers": {
        "Content-Type": "application/pdf"
    },
    "body": "JVBERi0xLjQKJZOMi54gUmVwb3J0TG..."
}
```


## Testing

[pytest](https://pytest.org) is used for unit tests. 

As generating the PDF has a dependency on S3, [moto](https://github.com/spulec/moto) is used for mocking S3.