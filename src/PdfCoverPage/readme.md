# PDF Cover Page

This is a Flask app run as via Docker image.

It has the following endpoints:

* `/pdfcoverpage/{bnumber}` - generate PDF cover-page using manifest for specified bnumber.
* `/pdfcoverpage/ping` - for health-checks

## Implementation Notes

All data for the cover-page will be taken from the Manifest which is stored in S3. 

If the manifest does not exist the call will fail with a 404.

Uses [reportLab](https://www.reportlab.com/) library for PDF generation.

Uses open source [inter](https://fonts.google.com/specimen/Inter) as built in fonts for reportLab do not render all required characters correctly. 

## Running Locally

This can be run via Docker with (dockerfile in root of repo):

```bash 
# build docker image
docker build -t pdfgen:local -f Dockerfile-pdfgenerator .

# run container
docker run --rm -it --name pdfgen -p 8080:8000 pdfgen:local

# needs access to S3 AWS so will need to provide aws credentials
# done via env_vars or (simpler) mounting .aws folder
docker run --rm -it --name pdfgen -p 8080:8000 -v $HOME\.aws:/root/.aws:ro --env AWS_PROFILE=wcdev pdfgen:local 
```