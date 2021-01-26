import json
import io
import os
from botocore.exceptions import ClientError
from http import HTTPStatus

from reportlab.lib import colors
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table
from reportlab.platypus.flowables import TopPadder
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.pagesizes import A4

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
    manifest = get_manifest(identifier)

    if not manifest:
        print(f"could not find PDF cover-page for '{identifier}'")
        return generate_response(HTTPStatus.NOT_FOUND, "Manifest for identifier not found",
                                 {"Content-Type": "text/plain"})

    print(f"generating PDF cover-page for '{identifier}'")

    # take the pertinent fields and flatten them to make it easier to use
    relevant_fields = {}
    for k, v in manifest.items():
        if k in ["label", "summary"]:
            relevant_fields[k] = get_first_lang_value(v)
        elif k == "metadata":
            flattened = {}
            for d in v:
                label = get_first_lang_value(d["label"])
                value = get_first_lang_value(d["value"])
                flattened[label] = value
            relevant_fields[k] = flattened
        elif k == "requiredStatement":
            value = v["value"]
            relevant_fields[k] = v["value"][get_first_lang(value)]
        elif k == "provider":
            relevant_fields[k] = v[0]

    # render the PDF
    pdf = build_pdf(identifier, relevant_fields)

    # generate PDF and return base64 encoded
    return {}


def build_pdf(identifier: str, data: dict):
    # TODO - pass binary stream rather than filename to write to?

    pdf_elements = []
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(name="Heading", fontName="Helvetica-Bold", fontSize=18, leading=20))
    styles.add(ParagraphStyle(name="SubHeading", fontName="Helvetica-Bold", fontSize=14, leading=16))
    styles.add(ParagraphStyle(name="Footer", fontName="Helvetica", fontSize=13, leading=18))
    normal = ParagraphStyle("Normal")
    normal.fontSize = 14
    normal.leading = 16

    label = data.get("label", "---NO TITLE---")
    pdf_elements.append(Paragraph(label, styles["Heading"]))
    pdf_elements.append(Spacer(1, 30))

    for k, v in data["metadata"].items():
        pdf_elements.append(Paragraph(k, styles["SubHeading"]))
        pdf_elements.append(Spacer(1, 10))
        pdf_elements.append(Paragraph(v, normal))
        pdf_elements.append(Spacer(1, 10))
    pdf_elements.append(Spacer(1, 20))

    requiredStatement = data["requiredStatement"]
    for stmt in requiredStatement:
        pdf_elements.append(Paragraph(stmt, normal))

    # bottom section
    provider = data["provider"]

    provider_label = provider["label"]
    address_parts = provider_label[get_first_lang(provider_label)]

    logo_url = provider["logo"][0]["id"]

    # create a table of 2 columns
    # the first has the image, the seconds has multiple rows - 1 per address part
    bottom = Table([(
        Paragraph(f"<img src='{logo_url}' height='66' width='200'/>"),
        [Paragraph(part, styles["Footer"]) for part in address_parts])])
    bottom.setStyle([
        ("VALIGN", (0, 0), (1, 1), "BOTTOM")
    ])
    pdf_elements.append(TopPadder(bottom))

    doc = SimpleDocTemplate(f"{identifier}.pdf", pagesize=A4, font="Helvetica", topMargin=30, bottomMargin=30)
    doc.build(pdf_elements)


def get_first_lang_value(el):
    lang = next(iter(el))
    return el[lang][0]


def get_first_lang(el):
    return next(iter(el))


def get_manifest(identifier: str) -> dict:
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
