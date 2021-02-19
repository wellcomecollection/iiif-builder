import asyncio
import aiohttp
from logzero import logger

ORIGINAL_FORMAT = "https://wellcomelibrary.org/iiif/{bnum}/manifest"
NEW_FORMAT = "http://localhost:8084/presentation/v2/{bnum}"

rules = {
    "": {
        "ignore": ["@id", "label", "metadata", "logo", "service"],
        # "contains" with label, metadata a mess, service complicated
        "extra_new": ["thumbnail", "attribution", "within"],
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
        "extra_orig": ["protocol"]
    },
    "sequences-canvases-seeAlso": {
        "ignore": ["@id"],
    },
    "sequences-canvases-images": {
        "ignore": ["@id", "on"],
    },
    "sequences-canvases-images-resource": {
        "ignore": ["@id"],
        "ignore_length": ["service"]  # original duplicates auth-services
    },
    "sequences-canvases-images-resource-service": {
        "version_insensitive": ["@id", "profile"],
        "extra_new": ["width", "height"],
        "ignore_length": ["service"]  # original duplicates auth-services
    },
    "sequences-canvases-images-resource-service-service": {
        "version_insensitive": ["profile"],
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
        "version_insensitive": ["@context"]
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
    }
}


class Comparer:
    is_authed = False

    def compare_manifests(self, bnumber, original, new):
        logger.info(f"Comparing {bnumber}")
        self.is_authed = False

        # do a "Contains" check for label
        are_equal = True
        if original["label"] not in new["label"]:
            logger.warning(f"Original and new label differ. {original['label']} - {new['label']}")
            are_equal = False

        # services are finnicky - handle separately
        are_equal = self.compare_services(original["service"], new["service"]) and are_equal

        # fall through
        are_equal = self.dictionary_comparison(original, new, "") and are_equal

        return are_equal

    def compare_services(self, orig, new):
        # build new dict by key as these can be in funny order
        are_equal = True

        def get_svc_list(svcs):
            if not isinstance(svcs, list):
                svcs = [svcs]

            output = {}
            for s in svcs:
                profile = s["profile"]
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

    def get_next_level(self, current, next):
        return f"{current}-{next}" if current else next

    def dictionary_comparison(self, orig, new, level):
        orig_keys = orig.keys()
        new_keys = new.keys()
        # logger.debug(f"processing {level}")

        are_equal = True
        rules_for_level = rules.get(level, {})
        ignore = rules_for_level.get("ignore", [])
        version_insensitive = rules_for_level.get("version_insensitive", [])
        expected_extra_new = rules_for_level.get("extra_new", [])
        expected_extra_orig = rules_for_level.get("extra_orig", [])
        size_only = rules_for_level.get("size_only", [])
        ignore_length = rules_for_level.get("ignore_length", [])
        domain_insensitive = rules_for_level.get("domain_insensitive", [])

        if orig_extra := orig_keys - new_keys:
            if unexpected_extra := [e for e in orig_extra if e not in expected_extra_orig]:
                logger.warning(f"Original has additional keys {','.join(unexpected_extra)} at {level}")
                are_equal = False

        if new_extra := new_keys - orig_keys:
            if unexpected_extra := [e for e in new_extra if e not in expected_extra_new]:
                logger.warning(f"New has additional keys {','.join(unexpected_extra)} at {level}")
                are_equal = False

        def compare_elements(eq, k, lvl, o, n):
            if isinstance(o, dict) and isinstance(n, dict):
                eq = self.dictionary_comparison(o, n, self.get_next_level(lvl, k)) and eq
            elif isinstance(o, dict) or isinstance(n, dict):
                logger.warning(f"{k} at {lvl} have mismatching type - one is dict")
                eq = False
            else:
                o_v = self.single_or_first(o)
                n_v = self.single_or_first(n)
                if k in version_insensitive:
                    if not self.version_insensitive_compare(o_v, n_v):
                        logger.warning(f"{k} at {lvl} don't pass version_insensitive_compare: '{o_v}' - '{n_v}'")
                        eq = False
                elif k in domain_insensitive:
                    if not self.domain_insensitive_compare(o_v, n_v):
                        logger.warning(f"{k} at {lvl} don't pass domain_insensitive_compare: '{o_v}' - '{n_v}'")
                        eq = False
                elif o_v != n_v:
                    # old P2 shows largest Width and Height in "sequences-canvases-images-resource"
                    # however, if auth the new will show the largest available

                    if self.is_authed and lvl == "sequences-canvases-images-resource" and key in ["width",
                                                                                                  "height"] and o_v > n_v:
                        # logger.debug(f"{k} at {lvl} don't match: '{o_v}' - '{n_v}' but this is due to auth")
                        pass
                    else:
                        logger.warning(f"{k} at {lvl} don't match: '{o_v}' - '{n_v}'")
                        eq = False
            return eq

        for key in [k for k in orig_keys if k not in ignore]:
            if key == "authService":
                logger.debug("Manifest is authed")
                self.is_authed = True

            o = orig[key]
            n = new[key]

            if isinstance(o, dict) and isinstance(n, list):
                o = [o]
            if isinstance(o, list) and isinstance(n, dict):
                n = [n]

            if self.is_authed and level == "sequences-canvases-images-resource" and key == "service" and len(o) == 3:
                continue

            if isinstance(o, list) and isinstance(n, list):
                if key not in ignore_length and len(o) != len(n):
                    logger.warning(f"{key} at {level} are lists of different length")
                    are_equal = False
                elif key not in size_only:  # size check is enough
                    for i in range(0, len(o)):
                        # if we are ignoring length, check what we can but don't exceed boundaries. Assumes orig is longer
                        if key in ignore_length and len(n) <= i:
                            continue
                        else:
                            o_i = o[i]
                            # This is to handle a bug in original where auth content is output twice at this level,
                            # with missing elements confirmLabel, header, failureHeader and failureDescription
                            if level == "sequences-canvases-images-resource" and "auth" in o_i["@id"] and "header" not in o_i:
                                logger.debug(f"Skipping element {i} at {level} as it is auth but missing")
                            else:
                                are_equal = compare_elements(are_equal, key, level, o_i, n[i]) and are_equal
            else:
                are_equal = compare_elements(are_equal, key, level, o, n) and are_equal

        return are_equal

    def single_or_first(self, val):
        """
        this is to handle a specific case where ['val'] in 1 manifest but 'val' in other
        it's really only for thumbnail>service>profile
        """

        if isinstance(val, list):
            # if len(val) > 1:
            #     raise ValueError(f"{val} has expected length of 1")
            return val[0]

        return val

    def version_insensitive_compare(self, orig, new):

        if orig == "http://iiif.io/api/auth/0/login/clickthrough" and new == "http://iiif.io/api/auth/1/clickthrough":
            return True

        # split into component parts
        orig_parts = orig.split("/")
        new_parts = new.split("/")

        # get any diffs, there should be 1 diff that is '5' (prod space)
        if diffs := list(set(orig_parts) - set(new_parts)):
            return len(diffs) == 1

        # if here, no diffs so they are teh same
        return True

    def domain_insensitive_compare(self, orig, new):
        # first 3 will be ["https", "", "domain.com"]
        return orig.split("/")[3:] == new.split("/")[3:]


async def run_comparison(bnumbers):
    comparer = Comparer()
    async with aiohttp.ClientSession(raise_for_status=True) as session:
        for bnumber in bnumbers:
            original = await load_manifest(session, bnumber, True)
            new = await load_manifest(session, bnumber, False)

            if comparer.compare_manifests(bnumber, original, new):
                logger.info(f"{bnumber} passed")
            else:
                logger.info(f"{bnumber} failed")


async def load_manifest(session, bnumber, is_original):
    format = ORIGINAL_FORMAT if is_original else NEW_FORMAT
    uri = format.replace("{bnum}", bnumber)

    async with session.get(uri) as response:
        return await response.json()


# Press the green button in the gutter to run the script.
if __name__ == '__main__':
    bnums = [
        #    "b29182608",
        "b19348216"
    ]

    asyncio.run(run_comparison(bnums))
    # asyncio.run(run_comparison('b19348216'))
