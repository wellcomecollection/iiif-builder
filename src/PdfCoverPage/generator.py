import json
import io
import os
import boto3

from botocore.exceptions import ClientError
from http import HTTPStatus
from flask import Flask, jsonify, send_file

from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table
from reportlab.platypus.flowables import TopPadder
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.pagesizes import A4

app = Flask(__name__)

region = os.environ.get("AWS_REGION", "eu-west-1")
manifest_bucket = os.environ.get("MANIFEST_BUCKET", "wellcomecollection-stage-iiif-presentation")
key_prefix = os.environ.get("KEY_PREFIX", "v3")


@app.route('/pdf-cover/<string:identifier>', methods=["GET"])
def generate_pdf(identifier: str):
    """Uses data from S3 manifest to construct PDF response"""
    manifest = get_manifest(identifier)

    if not manifest:
        print(f"could not find manifest for '{identifier}'")
        return {
                   "statusDescription": f'{HTTPStatus.NOT_FOUND.value} - {HTTPStatus.NOT_FOUND.phrase}',
                   "body": "Manifest for identifier not found",
               }, 404

    print(f"generating PDF cover-page for '{identifier}'")

    # take the pertinent fields and flatten them to make easier to use
    relevant_fields = extract_required_fields(manifest)

    # generate PDF and get bytes
    pdf_bytes = build_pdf(relevant_fields)
    pdf_bytes.seek(0)
    print(f"generated PDF cover-page for '{identifier}'")

    return send_file(pdf_bytes, mimetype="application/pdf")


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
            relevant_fields[k] = v
    return relevant_fields


def build_pdf(data: dict):
    """build pdf and return bytes"""

    # configure document
    register_fonts()
    styles = configure_stylesheet()
    normal = configure_normal_style()

    # build elements
    pdf_elements = []
    label_vals = data.get("label", ["---NO TITLE---"])
    pdf_elements.append(Paragraph(" - ".join(label_vals), styles["Heading"]))
    pdf_elements.append(Spacer(1, 30))

    def add_pdf_metadata(header, value):
        """Add header + value to pdf_elements, handling spacing etc"""
        pdf_elements.append(Paragraph(header, styles["Heading"]))
        pdf_elements.append(Spacer(1, 6))
        if isinstance(value, list):
            pdf_elements.extend([Paragraph(val, normal) for val in value])
        else:
            pdf_elements.append(Paragraph(value, normal))

        pdf_elements.append(Spacer(1, 10))

    # prefer requiredStatement for "license + attribution"
    attribution_statement = data.get("requiredStatement", [])

    if metadata := data.get("metadata", {}):
        if contributors := metadata.get("Contributors", []):
            add_pdf_metadata("Contributors", contributors)

        if pub_creation := metadata.get("Publication/creation", []):
            add_pdf_metadata("Publication/Creation", pub_creation)

        # fallback to "Attribution and usage" metadata for "license + attribution" if no requiredStatement
        if not attribution_statement:
            attribution_statement = metadata.get("Attribution and usage", [])

    add_pdf_metadata("Persistent URL", data["homepage"])

    # get providers - wellcome will always be first
    providers = data["provider"]
    wellcome_provider = providers[0]

    if len(providers) > 1:
        for additional_provider in providers[1:]:
            label = additional_provider["label"]
            add_pdf_metadata("Provider", label[get_first_lang(label)])

    # separator between metadata + rights statement
    pdf_elements.append(Spacer(1, 20))

    # take any element from requiredStatement/"Attribution and Usage" that is not "Wellcome Collection"
    if required_statement := [s for s in attribution_statement if s != "Wellcome Collection"]:
        add_pdf_metadata("License and attribution", required_statement)

    # bottom section
    provider_label = wellcome_provider["label"]
    address_parts = provider_label[get_first_lang(provider_label)]

    logo_url = wellcome_provider["logo"][0]["id"]

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


def configure_normal_style():
    normal = ParagraphStyle("Normal")
    normal.fontSize = 12
    normal.leading = 14
    normal.fontName = "Inter"
    return normal


def configure_stylesheet():
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(name="Heading", fontName="Inter-Bold", fontSize=12, leading=15))
    styles.add(ParagraphStyle(name="Footer", fontName="Inter", fontSize=11, leading=13))
    return styles


def register_fonts():
    pdfmetrics.registerFont(TTFont("Inter", "fonts/Inter-Regular.ttf"))
    pdfmetrics.registerFont(TTFont("Inter-Bold", "fonts/Inter-Bold.ttf"))


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
    try:
        client = boto3.client("s3", region)
        response = client.get_object(Bucket=manifest_bucket, Key=f"{key_prefix}/{identifier}")
        manifest = response.get("Body", "").read().decode()
        return json.loads(manifest)
    except ClientError as e:
        if e.response["Error"]["Code"] == "NoSuchKey":
            return {}

        raise


@app.errorhandler(404)
def page_not_found(e):
    return jsonify({"Error": "Resource not found"}), 404


@app.route('/pdf-cover/ping', methods=["GET"])
def ping():
    return jsonify(status='working')


if __name__ == '__main__':
    app.run(debug=True)
