import base64
import json
import io
import os
import boto3

from botocore.exceptions import ClientError
from http import HTTPStatus

from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table
from reportlab.platypus.flowables import TopPadder
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.pagesizes import A4



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
    """Uses data from S3 manifest to construct PDF response"""
    manifest = get_manifest(identifier)

    if not manifest:
        print(f"could not find manifest for '{identifier}'")
        return generate_response(HTTPStatus.NOT_FOUND, "Manifest for identifier not found",
                                 {"Content-Type": "text/plain"})

    print(f"generating PDF cover-page for '{identifier}'")

    # take the pertinent fields and flatten them to make easier to use
    relevant_fields = extract_required_fields(manifest)

    # generate PDF and get bytes
    pdf_bytes = build_pdf(relevant_fields)
    print(f"generated PDF cover-page for '{identifier}'")

    return generate_response(HTTPStatus.OK, base64.b64encode(pdf_bytes.getvalue()).decode(),
                             {"Content-Type": "application/pdf"}, True)


def extract_required_fields(manifest):
    """Process manifest and extract only those fields we are interested in"""
    relevant_fields = {}
    for k, v in manifest.items():
        if k == "label":
            relevant_fields[k] = get_first_lang_value(v)
        elif k == "homepage":
            relevant_fields[k] = v[0]["id"]
        elif k == "metadata":
            flattened = {}
            for d in v:
                label = get_first_lang_value(d["label"])[0]
                value = get_first_lang_value(d["value"])
                flattened[label] = value
            relevant_fields[k] = flattened
        elif k == "requiredStatement":
            value = v["value"]
            relevant_fields[k] = v["value"][get_first_lang(value)]
        elif k == "provider":
            relevant_fields[k] = v[0]
    return relevant_fields


def build_pdf(data: dict):
    """build pdf and return bytes"""

    # configure document
    pdfmetrics.registerFont(TTFont("Inter", "fonts/Inter-Regular.ttf"))
    pdfmetrics.registerFont(TTFont("Inter-Bold", "fonts/Inter-Bold.ttf"))
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(name="Heading", fontName="Inter-Bold", fontSize=12, leading=15))
    styles.add(ParagraphStyle(name="Footer", fontName="Inter", fontSize=11, leading=13))
    normal = ParagraphStyle("Normal")
    normal.fontSize = 12
    normal.leading = 14
    normal.fontName = "Inter"

    # build elements
    pdf_elements = []
    label_vals = data.get("label", ["---NO TITLE---"])
    pdf_elements.append(Paragraph(" - ".join(label_vals), styles["Heading"]))
    pdf_elements.append(Spacer(1, 30))

    def add_metadata(header, value):
        pdf_elements.append(Paragraph(header, styles["Heading"]))
        pdf_elements.append(Spacer(1, 6))
        if isinstance(value, list):
            pdf_elements.extend([Paragraph(val, normal) for val in value])
        else:
            pdf_elements.append(Paragraph(value, normal))

        pdf_elements.append(Spacer(1, 10))

    if metadata := data.get("metadata", {}):
        if contributors := metadata.get("Contributors", []):
            add_metadata("Contributors", contributors)

        if pub_creation := metadata.get("Publication/creation", []):
            add_metadata("Publication/Creation", pub_creation)

    add_metadata("Persistent URL", data["homepage"])
    pdf_elements.append(Spacer(1, 20))

    # take the first element that is not "Wellcome Collection"
    if required_statement := [s for s in data.get("requiredStatement", []) if s != "Wellcome Collection"]:
        add_metadata("License and attribution", required_statement[0])

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
        ("BOTTOMPADDING", (0, 0), (0, 0), 10)
    ])
    pdf_elements.append(TopPadder(bottom))

    pdf_bytes = io.BytesIO()
    doc = SimpleDocTemplate(pdf_bytes, pagesize=A4, topMargin=30, bottomMargin=30)
    doc.build(pdf_elements)
    return pdf_bytes


def get_first_lang_value(el):
    lang = get_first_lang(el)
    return el[lang]


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
    print(json.dumps(result))

    if result["isBase64Encoded"]:
        contents = base64.b64decode(result["body"])
        filename = f'{event.get("queryStringParameters", {}).get("identifier", "")}.pdf'
        with open(filename, "wb") as f:
            f.write(contents)
        print(f"Saved file '{filename}'")
