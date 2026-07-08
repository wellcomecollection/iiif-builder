import json
import math

import settings
import requests

def process():
    with open(settings.ALL_MANIFESTS_WITH_AUTH) as f:
        all_manifests = json.load(f)

    # all_manifests is a list in descending date order.
    # We want to find the point at which they start showing IIIF Auth v2 services as well as v1.
    max_index = len(all_manifests) - 1
    print(f"There are {max_index + 1} manifests without authorization")

    index = -1
    lower = 0
    upper = max_index

    while True:
        prev_try_index = index
        index = math.floor(lower + (upper - lower) / 2)
        if index == prev_try_index:
            print(f"Found cutoff point at index {index}/{max_index}")
            break
        manifest_slug = all_manifests[index]['manifestation_identifier']
        print("----------------")
        print(f"Trying index {index} of {max_index}: {manifest_slug}")
        print(f"Processed: {all_manifests[index]['processed']}")
        manifest_resp = requests.get(f"https://iiif.wellcomecollection.org/presentation/{manifest_slug}")
        manifest = manifest_resp.json()
        has_auth_cookie_service_1 = False
        has_auth_access_service_2 = False
        for service in manifest['services']:
            if service.get('@type', None) == 'AuthCookieService1':
                has_auth_cookie_service_1 = True
            if service.get('type', None) == 'AuthAccessService2':
                has_auth_access_service_2 = True

        if has_auth_cookie_service_1 is True and has_auth_access_service_2 is True:
            lower = index
            print(f"{manifest_slug} has Auth 2")
        elif has_auth_cookie_service_1 is True and has_auth_access_service_2 is False:
            upper = index
            print(f"{manifest_slug} does not have Auth 2")
        else:
            print(f"ERROR - no auth services in {manifest_slug}")
            break


if __name__ == '__main__':
    process()





