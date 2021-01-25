# PDF Cover Page

PDF Cover Page generator. This is run as an AWS Lambda Python function and called through ELB.

The function will take an `?identifier=` query string parameter and generate the PDF cover-page for that identifier.

All data for the cover-page will be taken from the Manifest, stored in S3. If the manifest does not exist the call will fail.

## Running Locally

The function can be run normal via:

```bash 
python generator.py
```

This will read the contents of `event.json` to simulate the event data that will be sent to the lambda function via ELB. See [AWS Docs](https://docs.aws.amazon.com/lambda/latest/dg/lambda-services.html) for more details on the shape of the event object.

Edit the `queryStringParameters` property to simulate passing an identifier.

```json
"queryStringParameters": {
  "identifier": "b24967646"
},
```

## Testing

[pytest](https://pytest.org) is used for unit tests. 

As generating the PDF has a dependency on S3, [moto](https://github.com/spulec/moto) is used for mocking S3.