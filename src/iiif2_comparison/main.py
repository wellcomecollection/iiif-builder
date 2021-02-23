import asyncio
import aiohttp
import logging
import logzero
from logzero import logger

logzero.loglevel(logging.INFO)

ORIGINAL_FORMAT = "https://wellcomelibrary.org/iiif/{bnum}/manifest"
NEW_FORMAT = "http://localhost:8084/presentation/v2/{bnum}"

rules = {
    "": {
        # metadata + seeAlso massively different
        "ignore": ["@id", "label", "metadata", "logo", "service", "seeAlso", "within", "license"],
        "extra_new": ["thumbnail", "attribution", "within", "description"],
        "extra_orig": ["service"]  # TODO - this will be fixed in #5034
    },
    "related": {
        "ignore": ["@id"],
        "extra_new": ["label"]
    },
    "sequences": {
        "ignore": ["@id"],
        "version_insensitive": ["label"],
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
        "extra_new": ["width", "height", "protocol"],  # TODO - is protocol too much here?
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
        "ignore": ["@id"],
        "order_by": "@id",
        "bnumber_insensitive": ["label"]  # required for manifests in collections
    },
    "service:search": {
        "ignore": ["@id"],
        "version_insensitive": ["@context", "profile"]
    },
    "service:search-service": {
        "ignore": ["@id", "label"],
        "version_insensitive": ["profile"]
    },
    "service:clickthrough": {
        "domain_insensitive": ["@id", "profile"]
    },
    "service:clickthrough-authService": {
        "ignore": ["profile"]
    },
    "service:clickthrough-authService-service": {
        "version_insensitive": ["profile"]
    },
    "manifests": {  # collections only
        "ignore": ["thumbnail", "@id"],
        "extra_new": ["thumbnail"],
        "version_insensitive": ["label"]
    }
}


class Comparer:
    _is_authed = False
    warnings = []
    failures = []

    def __init__(self, loader):
        self._loader = loader

    async def start_comparison(self, original, new, identifier=None):
        self._is_authed = False
        self.warnings = []
        self.failures = []

        if identifier:
            logger.info(f"Comparing {identifier}")

        return await self.run_comparison(original, new, identifier)

    async def run_comparison(self, original, new, identifier=None):
        original_type = original["@type"]
        if original_type != new["@type"]:
            self.failures.append("Mismatching type")
            return False

        if original_type == "sc:Manifest":
            logger.debug(f"{identifier} is a manifest..")
            return self.compare_manifests(original, new)

        elif original_type == "sc:Collection":
            logger.debug(f"{identifier} is a collection..")
            return await self.compare_collections(original, new)

    async def compare_collections(self, original, new):
        # TODO clean auth like manifests?

        # do a "Contains" check for label
        are_equal = True
        self.compare_label(new, original)

        # services are finnicky - handle separately
        are_equal = self.compare_services(original.get("service", {}), new.get("service", {})) and are_equal

        # default comparison
        are_equal = self.dictionary_comparison(original, new, "") and are_equal

        # find manifest @id and get data and compare
        are_equal = await self.compare_embedded_manifests(original.get("manifests", []),
                                                          new.get("manifests", [])) and are_equal

        return are_equal

    async def compare_embedded_manifests(self, original_manifests, new_manifests):
        original_len = len(original_manifests)
        new_len = len(new_manifests)

        if original_len != new_len:
            self.failures.append("manifest counts differ")
            return False

        success = True

        # iterate through manifests[], fetch each manifest and compare
        for i in range(0, original_len):
            logger.debug(f"Comparing manifest {i}")
            o_id = original_manifests[i].get("@id", None)
            n_id = new_manifests[i].get("@id", None)

            o_mani = await self._loader.fetch(o_id)
            n_mani = await self._loader.fetch(n_id)

            if not await self.run_comparison(o_mani, n_mani):
                self.failures.append(f"manifest[{i}] are not equal")
                success = False

        return success

    def compare_manifests(self, original, new):
        original_services = original.get("service", [])
        for s in original_services:
            if "authService" in s:
                logger.debug("Manifest is authed.. cleaning up original")
                self._is_authed = True
                self.clean_auth(original)

        # do a "Contains" check for label
        are_equal = True
        self.compare_label(new, original)

        # services are finnicky - handle separately
        are_equal = self.compare_services(original_services, new.get("service", {})) and are_equal

        # fall through
        are_equal = self.dictionary_comparison(original, new, "") and are_equal

        return are_equal

    def compare_label(self, new, original):
        orig_label = original["label"]
        new_label = new["label"]
        if orig_label != new_label and orig_label not in new_label:
            logger.debug(f"'_root_'.'label' mismatch: {orig_label} - {new_label}")
            self.warnings.append("'_root_'.'label' mismatch")

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
                    access_hint = s.get("accessHint", "")
                    if access_hint == "open":  # should this be in new?
                        output["open-access"] = s
                    elif access_hint == "clickthrough":
                        output["clickthrough"] = s
                    elif access_hint == "external":
                        output["external"] = s
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
            self.warnings.append(f"service are different lengths: {len(orig_services)} - {len(new_services)}")
            logger.debug(f"service are different lengths: {len(orig_services)} - {len(new_services)}")

        for k, o in orig_services.items():
            n = new_services.get(k, {})
            if not n:
                # expect new to always be smaller so not finding a service isn't an issue
                logger.debug(f"service of type '{k}' not found in new")
            else:
                are_equal = self.dictionary_comparison(o, n, f"service:{k}") and are_equal

        return are_equal

    def dictionary_comparison(self, orig, new, level):
        """
        Iterate through all keys in provided dictionaries, using predefined rules to determine if they are equal
        :param orig: dict of original wl.org item at current level
        :param new: dict of new wl.org item at current level
        :param level: level of dict being interrogated, used to lookup rules and for logging
        :return: boolean value representing whether provided dictionaries are equal
        """

        are_equal = True
        rules_for_level = rules.get(level, {})
        ignore = rules_for_level.get("ignore", [])
        level_for_logs = level if level else '_root_'

        expected_extra_new = rules_for_level.get("extra_new", [])
        expected_extra_orig = rules_for_level.get("extra_orig", [])
        size_only = rules_for_level.get("size_only", [])

        # check for the existence of
        orig_keys = orig.keys()
        new_keys = new.keys()
        if orig_extra := orig_keys - new_keys:
            if unexpected_extra := [e for e in orig_extra if e not in expected_extra_orig and orig[e]]:
                self.failures.append(f"Original '{level_for_logs}' has unexpected keys '{','.join(unexpected_extra)}'")
                logger.debug(f"Original '{level_for_logs}' has unexpected keys '{','.join(unexpected_extra)}'")
                are_equal = False

        if new_extra := new_keys - orig_keys:
            if unexpected_extra := [e for e in new_extra if e not in expected_extra_new and new[e]]:
                self.failures.append(f"New '{level_for_logs}' has unexpected keys '{','.join(unexpected_extra)}'")
                logger.debug(f"New '{level_for_logs}' has unexpected keys '{','.join(unexpected_extra)}'")
                are_equal = False

        for key in [k for k in orig_keys if k not in ignore]:
            o = orig.get(key, "")
            n = new.get(key, "")

            if isinstance(o, dict) and isinstance(n, list):
                o = [o]
            if isinstance(o, list) and isinstance(n, dict):
                n = [n]
            if isinstance(o, list) and isinstance(n, list):
                # if the 'next' level has an orderBy rule, reorder before compare
                if order_by := rules.get(self.get_next_level(level, key), {}).get("order_by", ""):
                    o = sorted(o, key=lambda item: item[order_by])
                    n = sorted(n, key=lambda item: item[order_by])

                if len(o) != len(n):
                    self.failures.append(f"'{level_for_logs}'.'{key}' lists of different length")
                    logger.debug(f"'{level_for_logs}'.'{key}' lists of different length: {len(o)} - {len(n)}")
                    are_equal = False
                elif key not in size_only:  # size check is enough
                    for i in range(0, len(o)):
                        are_equal = self.compare_elements(key, level, o[i], n[i]) and are_equal
            else:
                are_equal = self.compare_elements(key, level, o, n) and are_equal

        return are_equal

    def compare_elements(self, key, level, orig, new):
        """
        Compare individual elements in manifest, using predefined rules to determine if they are equal
        :param key: key of item in dictionary being interrogated
        :param level: level of dict being interrogated, used to lookup rules and for logging
        :param orig: item for key at current level from original wl.org manifest
        :param new: item for key at current level from new manifest
        :return: boolean value representing whether provided dictionaries are equal
        """

        rules_for_level = rules.get(level, {})
        level_for_logs = level if level else '_root_'
        version_insensitive = rules_for_level.get("version_insensitive", [])
        domain_insensitive = rules_for_level.get("domain_insensitive", [])
        bnumber_insensitive = rules_for_level.get("bnumber_insensitive", [])

        if isinstance(orig, dict) and isinstance(new, dict):
            return self.dictionary_comparison(orig, new, self.get_next_level(level, key))
        elif isinstance(orig, dict) or isinstance(new, dict):
            self.failures.append(f"'{level_for_logs}'.'{key}' type mismatch")
            logger.debug(f"'{level_for_logs}'.'{key}' type mismatch: {type(orig)} - {type(new)}")
            return False
        else:
            o_v = self.single_or_first(orig)
            n_v = self.single_or_first(new)
            if key in version_insensitive:
                if not self.version_insensitive_compare(o_v, n_v):
                    self.failures.append(f"'{level_for_logs}'.'{key}' failed version-insensitive compare")
                    logger.debug(f"'{level_for_logs}'.'{key}' failed version-insensitive comparison: '{o_v}' - '{n_v}'")
                    return False
            elif key in domain_insensitive:
                if not self.domain_insensitive_compare(o_v, n_v):
                    self.failures.append(f"'{level_for_logs}'.'{key}' failed domain-insensitive compare")
                    logger.debug(f"'{level_for_logs}'.'{key}' failed domain-insensitive comparison: '{o_v}' - '{n_v}'")
                    return False
            elif key in bnumber_insensitive:
                if not self.bnumber_insensitive_compare(o_v, n_v):
                    self.failures.append(f"'{level_for_logs}'.'{key}' failed bnumber-insensitive compare")
                    logger.debug(f"'{level_for_logs}'.'{key}' failed bnumber-insensitive comparison: '{o_v}' - '{n_v}'")
                    return False
            elif o_v != n_v:
                # old P2 shows largest Width and Height in "sequences-canvases-images-resource"
                # however, if auth the new will show the largest available
                if self._is_authed and level == "sequences-canvases-images-resource" and key in ["width",
                                                                                                 "height"] and o_v > n_v:
                    logger.debug(f"'{level_for_logs}'.'{key}' don't match due to auth: '{o_v}' - '{n_v}'")
                else:
                    logger.debug(f"'{level_for_logs}'.'{key}' failed comparison: '{o_v}' - '{n_v}'")
                    self.failures.append(f"'{level_for_logs}'.'{key}' failed comparison")
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

        separator = "/" if orig.startswith("http") else " "  # hmmm... yeah

        # split into component parts
        orig_parts = orig.split(separator)
        new_parts = new.split(separator)

        # get any diffs, there should be 1 diff
        if diffs := list(set(orig_parts) - set(new_parts)):
            return len(diffs) == 1

        # if here, no diffs so they are the same
        return True

    @staticmethod
    def domain_insensitive_compare(orig, new):
        # first 3 will be ["https", "", "domain.com"]
        return orig.split("/")[3:] == new.split("/")[3:]

    @staticmethod
    def bnumber_insensitive_compare(orig, new):
        # e.g. "All OCR-derived annotations for b24990796-66" and "All OCR-derived annotations for b24990796_0072"
        # should be deemed as equal

        # split into component parts
        orig_parts = orig.split(" ")
        new_parts = new.split(" ")

        if diffs := [d for d in list(set(orig_parts) - set(new_parts)) if not d.startswith("b")]:
            return len(diffs) == 1

        # if here, no diffs so they are the same
        return True


class Loader:
    async def __aenter__(self):
        self._session = aiohttp.ClientSession()
        return self

    async def __aexit__(self, *err):
        await self._session.close()
        self._session = None

    async def fetch_bnumber(self, bnumber, is_original):
        format = ORIGINAL_FORMAT if is_original else NEW_FORMAT
        uri = format.replace("{bnum}", bnumber)

        return await self.fetch(uri)

    async def fetch(self, uri):
        async with self._session.get(uri) as response:
            if 200 <= response.status < 300:
                response_json = await response.json(content_type=None)  # collections are coming back as text/plain
                if not response_json:
                    logger.error(f"{uri} returned nothing")
                    return {}
                else:
                    return response_json

            logger.error(f"Failed to get {uri} for comparison. Status {response.status}")
            return {}


async def main(bnums):
    failed = []
    passed = []

    async with Loader() as loader:
        comparer = Comparer(loader)
        count = 0
        for bnumber in await bnum_generator(bnums):
            count += 1
            original = await loader.fetch_bnumber(bnumber, True)
            new = await loader.fetch_bnumber(bnumber, False)

            if not original or not new:
                logger.info(f"{count}**{bnumber} failed")
                failed.append(bnumber)
                continue

            if await comparer.start_comparison(original, new, bnumber):
                passed.append(bnumber)
                logger.info(f"{count}**{bnumber} passed")
                if comparer.warnings:
                    logger.info("\n-".join(set(comparer.warnings)))
            else:
                failed.append(bnumber)
                logger.info(f"{count}**{bnumber} failed")
                logger.info("\n-".join(set(comparer.failures)))

    logger.info("*****************************")
    logger.info(f"passed ({len(passed)}): {','.join(passed)}")
    logger.info(f"failed ({len(failed)}): {','.join(failed)}")


async def bnum_generator(bnums):
    # Allows bnums to be a list or a file location
    # Just because you can, doesn't mean you should
    return (row.strip("\n") for row in open(bnums)) if isinstance(bnums, str) else bnums


# Press the green button in the gutter to run the script.
if __name__ == '__main__':
    bnums = ['b32497179', 'b32497167', 'b32497088', 'b19348216', 'b24967646', 'b29182608', 'b22454408', 'b2043067x',
             'b30136155', 'b19812292', 'b19812413', 'b31919996', 'b19977335', 'b24990796', 'b18031511', 'b10727000',
             'b24923333']
    file = r"C:\repos\wellcomecollection\iiif-builder\src\Wellcome.Dds\CatalogueClient\examples.txt"
    asyncio.run(main(['b19818786']))
