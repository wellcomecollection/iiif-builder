 # PDF Thumb Generator
 
One off process for generating thumbs for born digital PDFs.

This is a fairly brittle process and requires:
* A CSV of item numbers to process
* Ghostscript installed

Overall process is:
* Iterate over items in CSV, per item
* Find PDF file from mets
* Download PDF
* Use ghostscript to generate JPG
* Resize JPG to 1024 wide
* Upload to S3

Expected CSV format:

```
Identifier,Processed,Title
b32878382,14/07/2021 09:48,Mind the gap
```

## Config

For running, need to setup the following app settings:

* Dds-AWS
* Storage-AWS
* Storage_ClientId
* Storage_ClientSecret