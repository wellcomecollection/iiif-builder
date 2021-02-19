import asyncio
import traceback

import aiohttp
from logzero import logger

ORIGINAL_FORMAT = "https://wellcomelibrary.org/iiif/{bnum}/manifest"
NEW_FORMAT = "http://localhost:8084/presentation/v2/{bnum}"

rules = {
    "": {
        # metadata + seeAlso massively different
        "ignore": ["@id", "label", "metadata", "logo", "service", "seeAlso", "otherContent"],
        "extra_new": ["thumbnail", "attribution", "within", "description"],
    },
    "related": {
        "ignore": ["@id"],
        "extra_new": ["label"]
    },
    "sequences": {
        "ignore": ["@id"],
    },
    "sequences-rendering": {
        "ignore": ["@id", "label"],
    },
    "sequences-canvases": {
        "ignore": ["@id"],
    },
    "sequences-canvases-thumbnail": {
        "version_insensitive": ["@id"],
    },
    "sequences-canvases-thumbnail-service": {
        "version_insensitive": ["@id"],
        "ignore": ["protocol"],
        "extra_orig": ["protocol"]  # add to new?
    },
    "sequences-canvases-seeAlso": {
        "ignore": ["@id"],
    },
    "sequences-canvases-images": {
        "ignore": ["@id", "on"],
    },
    "sequences-canvases-images-resource": {
        "ignore": ["@id"],
    },
    "sequences-canvases-images-resource-service": {
        "version_insensitive": ["@id", "profile"],
        "extra_new": ["width", "height"],
    },
    "sequences-canvases-images-resource-service-service": {
        "version_insensitive": ["profile"],  # auth
    },
    "sequences-canvases-images-resource-service-service-service": {
        "version_insensitive": ["profile"],  # auth
    },
    "sequences-canvases-otherContent": {
        "ignore": ["@id", "label"],
    },
    "structures": {
        "ignore": ["@id"],
        "size_only": ["canvases"]
    },
    "otherContent": {
        "ignore": ["@id"]
    },
    "service:search": {
        "ignore": ["@id"],
        "version_insensitive": ["@context", "profile"]
    },
    "service:search-service": {
        "ignore": ["@id", "label"],
        "version_insensitive": ["profile"]
    },
    "service:auth": {
        "domain_insensitive": ["@id", "profile"]
    },
    "service:auth-authService": {
        "ignore": ["profile"]
    },
    "service:auth-authService-service": {
        "version_insensitive": ["profile"]
    },
}


class Comparer:
    is_authed = False

    def run_compare(self, bnumber, original, new):
        logger.info(f"Comparing {bnumber}")
        self.is_authed = False

        original_type = original["@type"]
        if original_type != new["@type"]:
            logger.warning(f"Type mismatch {original['@type']} - {new['@type']}")
            return False

        if original_type == "sc:Manifest":
            logger.info(f"{bnumber} is a manifest..")
            return self.compare_manifests(bnumber, original, new)

        elif original_type == "sc:Collection":
            logger.info(f"{bnumber} is a collection..")
            logger.warning("don't handle collections yet.. aborting")
            return False

    def compare_manifests(self, bnumber, original, new):
        original_services = original["service"]
        for s in original_services:
            if "authService" in s:
                logger.debug("Manifest is authed.. cleaning up original")
                self.is_authed = True
                self.clean_auth(original)

        # do a "Contains" check for label
        are_equal = True
        if original["label"] not in new["label"]:
            logger.warning(f"Original and new label differ. {original['label']} - {new['label']}")
            are_equal = False

        # services are finnicky - handle separately
        are_equal = self.compare_services(original_services, new.get("service", {})) and are_equal

        # otherContent can be in different order
        are_equal = self.compare_other_content(original.get("otherContent", []),
                                               new.get("otherContent", [])) and are_equal

        # fall through
        are_equal = self.dictionary_comparison(original, new, "") and are_equal

        return are_equal

    def clean_auth(self, original):
        # auth services are duplicated in the original in:
        # sequences[].canvases[].images[].resource.service[] AND
        # sequences[].canvases[].images[].resource.service["@type": "dctypes:Image"].service[]
        # this makes it very difficult to deal with so cleaning prior to handling
        # the duplicated element is not identical, one has missing elements. Remove the more sparse one.
        # missing elements: confirmLabel, header, failureHeader and failureDescription

        def clean_service_element(services):
            to_keep = []
            for svc in services:
                if isinstance(svc, str):
                    if svc not in to_keep:  # a simple string link to a svc "https://dlcs.io/auth/2/clickthrough",
                        to_keep.append(svc)
                elif "/image/" in svc["@context"]:  # this is an image service, process it's internal services element
                    svc["service"] = clean_service_element(svc["service"])
                    to_keep.append(svc)
                elif "auth" in svc["@id"] and "header" in svc:
                    to_keep.append(svc)

            return to_keep

        # for each canvas...
        for c in original["sequences"][0]["canvases"]:
            # iterate the images...
            for i in c["images"]:
                # get the resource
                resource = i["resource"]
                # iterate the services
                resource["service"] = clean_service_element(resource["service"])

    def compare_services(self, orig, new):
        # build new dict by key as these can be in funny order
        are_equal = True

        def get_svc_list(svcs):
            if not isinstance(svcs, list):
                svcs = [svcs]

            output = {}
            for s in svcs:
                profile = s.get("profile", "")
                if "access-control-hints" in profile:
                    if s.get("accessHint", "") == "open":  # should this be in new?
                        output["open-access"] = s
                    else:
                        output["auth"] = s
                elif "search" in profile:
                    output["search"] = s
                elif "tracking" in profile:
                    output["tracking"] = s
                elif "ui-extensions" in profile:
                    output["ui-extensions"] = s
            return output

        orig_services = get_svc_list(orig)
        new_services = get_svc_list(new)

        if len(orig_services) != len(new_services):
            logger.debug(f"service are different lengths: {len(orig_services)} - {len(new_services)}")

        for k, o in orig_services.items():
            n = new_services.get(k, {})
            if not n:
                # expect new to always be smaller so not finding a service isn't an issue
                logger.debug(f"service of type '{k}' not found in new")
            else:
                are_equal = self.dictionary_comparison(o, n, f"service:{k}") and are_equal

        return are_equal

    def compare_other_content(self, orig, new):
        # can be in any order
        orig_sorted = sorted(orig, key=lambda item: item["@id"])
        new_sorted = sorted(new, key=lambda item: item["@id"])

        if len(orig_sorted) != len(new_sorted):
            logger.warning(f"otherContent are lists of different length")
            return False

        are_equal = True
        for i in range(0, len(orig_sorted)):
            are_equal = self.compare_elements("", "otherContent", orig_sorted[i], new_sorted[i]) and are_equal

        return are_equal

    def dictionary_comparison(self, orig, new, level):
        orig_keys = orig.keys()
        new_keys = new.keys()

        are_equal = True
        rules_for_level = rules.get(level, {})
        ignore = rules_for_level.get("ignore", [])

        expected_extra_new = rules_for_level.get("extra_new", [])
        expected_extra_orig = rules_for_level.get("extra_orig", [])
        size_only = rules_for_level.get("size_only", [])
        ignore_length = rules_for_level.get("ignore_length", [])

        if orig_extra := orig_keys - new_keys:
            if unexpected_extra := [e for e in orig_extra if e not in expected_extra_orig]:
                logger.warning(f"Original has additional keys '{','.join(unexpected_extra)}' at {level}")
                are_equal = False

        if new_extra := new_keys - orig_keys:
            if unexpected_extra := [e for e in new_extra if e not in expected_extra_new]:
                logger.warning(f"New has additional keys '{','.join(unexpected_extra)}' at {level}")
                are_equal = False

        for key in [k for k in orig_keys if k not in ignore]:
            o = orig.get(key, "")
            n = new.get(key, "")

            if isinstance(o, dict) and isinstance(n, list):
                o = [o]
            if isinstance(o, list) and isinstance(n, dict):
                n = [n]
            if isinstance(o, list) and isinstance(n, list):
                if key not in ignore_length and len(o) != len(n):
                    logger.warning(f"{key} at {level} are lists of different length")
                    are_equal = False
                elif key not in size_only:  # size check is enough
                    for i in range(0, len(o)):
                        # if we are ignoring length, check what we can but don't exceed boundaries.
                        # Assumes orig is longer
                        if key in ignore_length and len(n) <= i:
                            continue
                        else:
                            are_equal = self.compare_elements(key, level, o[i], n[i]) and are_equal
            else:
                are_equal = self.compare_elements(key, level, o, n) and are_equal

        return are_equal

    def compare_elements(self, key, level, orig, new):
        rules_for_level = rules.get(level, {})
        version_insensitive = rules_for_level.get("version_insensitive", [])
        domain_insensitive = rules_for_level.get("domain_insensitive", [])

        if isinstance(orig, dict) and isinstance(new, dict):
            return self.dictionary_comparison(orig, new, self.get_next_level(level, key))
        elif isinstance(orig, dict) or isinstance(new, dict):
            logger.warning(f"{key} at {level} have mismatching type - one is dict")
            return False
        else:
            o_v = self.single_or_first(orig)
            n_v = self.single_or_first(new)
            if key in version_insensitive:
                if not self.version_insensitive_compare(o_v, n_v):
                    logger.warning(f"{key} at {level} fails version_insensitive_compare: '{o_v}' - '{n_v}'")
                    return False
            elif key in domain_insensitive:
                if not self.domain_insensitive_compare(o_v, n_v):
                    logger.warning(f"{key} at {level} fails domain_insensitive_compare: '{o_v}' - '{n_v}'")
                    return False
            elif o_v != n_v:
                # old P2 shows largest Width and Height in "sequences-canvases-images-resource"
                # however, if auth the new will show the largest available
                if self.is_authed and level == "sequences-canvases-images-resource" and key in ["width",
                                                                                                "height"] and o_v > n_v:
                    # logger.debug(f"{k} at {lvl} don't match: '{o_v}' - '{n_v}' but this is due to auth")
                    pass
                else:
                    logger.warning(f"{key} at {level} don't match: '{o_v}' - '{n_v}'")
                    return False
        return True

    @staticmethod
    def get_next_level(current, next):
        if not next:
            return current

        return f"{current}-{next}" if current else next

    @staticmethod
    def single_or_first(val):
        """
        this is to handle a specific case where ['val'] in 1 manifest but 'val' in other
        it's really only for thumbnail>service>profile
        """
        return val[0] if isinstance(val, list) else val

    @staticmethod
    def version_insensitive_compare(orig, new):

        if orig == "http://iiif.io/api/auth/0/login/clickthrough" and new == "http://iiif.io/api/auth/1/clickthrough":
            return True

        # split into component parts
        orig_parts = orig.split("/")
        new_parts = new.split("/")

        # get any diffs, there should be 1 diff
        if diffs := list(set(orig_parts) - set(new_parts)):
            return len(diffs) == 1

        # if here, no diffs so they are the same
        return True

    @staticmethod
    def domain_insensitive_compare(orig, new):
        # first 3 will be ["https", "", "domain.com"]
        return orig.split("/")[3:] == new.split("/")[3:]


async def run_comparison(bnumbers):
    comparer = Comparer()
    failed = []
    passed = []
    async with aiohttp.ClientSession(raise_for_status=True) as session:
        for bnumber in bnumbers:
            try:
                original = await load_manifest(session, bnumber, True)
                new = await load_manifest(session, bnumber, False)

                if comparer.run_compare(bnumber, original, new):
                    passed.append(bnumber)
                    logger.info(f"{bnumber} passed")
                else:
                    failed.append(bnumber)
                    logger.info(f"{bnumber} failed")
            except Exception:
                e = traceback.format_exc()
                logger.error(f"Error processing {bnumber}: {e}")

    logger.info("*****************************")
    logger.info(f"{','.join(passed)} passed")
    logger.info(f"{','.join(failed)} failed")


async def load_manifest(session, bnumber, is_original):
    format = ORIGINAL_FORMAT if is_original else NEW_FORMAT
    uri = format.replace("{bnum}", bnumber)

    async with session.get(uri) as response:
        return await response.json(content_type=None)  # collections are coming back as text/plain


# Press the green button in the gutter to run the script.
if __name__ == '__main__':
    # "b14583793", "b20641151", "b24963215",
    bnums = [
         "b32497179", "b32497167", "b32497088", "b19348216", "b24990796",
        "b32496485", "b14584463", "b24967646", "b29182608", "b10727000", "b2178081x", "b22454408", "b2043067x",
        "b24923333", "b30136155", "b19812292", "b19812413", "b18031511", "b31919996", "b19977335", ]

    asyncio.run(run_comparison(bnums))
    # asyncio.run(run_comparison(['b32497088']))
    # asyncio.run(run_comparison('b19348216'))
