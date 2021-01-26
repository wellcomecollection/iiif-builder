#! /bin/bash

# see https://docs.aws.amazon.com/lambda/latest/dg/python-package.html#python-package-dependencies
pip3 install --system --target ./package reportlab
cd package ; zip -r ../pdf_gen.zip . * ; cd ..
zip -g pdf_gen.zip generator.py
zip -g pdf_gen.zip fonts/Inter-Bold.ttf
zip -g pdf_gen.zip fonts/Inter-Regular.ttf