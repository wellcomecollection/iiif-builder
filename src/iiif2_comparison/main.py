import asyncio
import aiohttp
from logzero import logger

ORIGINAL_FORMAT = "https://wellcomelibrary.org/iiif/{bnum}/manifest"
NEW_FORMAT = "http://localhost:8084/presentation/v2/{bnum}"

rules = {
    "": {
        "ignore": ["@id", "label", "metadata", "logo"],  # "contains" with label, metadata a mess
        "extra_new": ["thumbnail", "attribution", "within"]
    },
    "-related": {
        "ignore": ["@id"],
        "extra_new": ["label"]
    },
    "-sequences": {
        "ignore": ["@id"],
    },
    "-sequences-rendering": {
        "ignore": ["@id", "label"],
    },
    "-sequences-canvases": {
        "ignore": ["@id"],
        "space_insensitive": []
    },
    "-sequences-canvases-thumbnail": {
        "space_insensitive": ["@id"],
    },
    "-sequences-canvases-thumbnail-service": {
        "space_insensitive": ["@id"],
        "ignore": ["protocol"],
        "extra_orig": ["protocol"]
    },
    "-sequences-canvases-seeAlso": {
        "ignore": ["@id"],
    },
    "-sequences-canvases-images": {
        "ignore": ["@id", "on"],
    },
    "-sequences-canvases-images-resource": {
        "ignore": ["@id"],
    },
    "-sequences-canvases-images-resource-service": {
        "space_insensitive": ["@id"],
        "extra_new": ["width", "height"],
    },
    "-sequences-canvases-otherContent": {
        "ignore": ["@id", "label"],
    },
    "-structures": {
        "ignore": ["@id"],
        "size_only": ["canvases"]
    },
    "-otherContent": {
        "ignore": ["@id"]
    }
}


def compare_manifests(bnumber, original, new):
    logger.info(f"Comparing {bnumber}")

    # do a "Contains" check for label
    are_equal = True
    if original["label"] not in new["label"]:
        logger.warning(f"Original and new label differ. {original['label']} - {new['label']}")
        are_equal = False

    are_equal = dictionary_comparison(original, new, "") and are_equal
    return are_equal


def dictionary_comparison(orig, new, level):
    orig_keys = orig.keys()
    new_keys = new.keys()

    are_equal = True
    rules_for_level = rules.get(level, {})
    ignore_for_level = rules_for_level.get("ignore", [])
    space_insensitive_for_level = rules_for_level.get("space_insensitive", [])
    expected_extra_new = rules_for_level.get("extra_new", [])
    expected_extra_orig = rules_for_level.get("extra_orig", [])
    size_only = rules_for_level.get("size_only", [])

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
            eq = dictionary_comparison(o, n, f"{lvl}-{k}") and eq
        elif isinstance(o, dict) or isinstance(n, dict):
            logger.warning(f"{k} at {lvl} have mismatching type - one is dict")
            eq = False
        else:
            o_v = single_or_first(o)
            n_v = single_or_first(n)
            if k in space_insensitive_for_level:
                if not space_insensitive_compare(o_v, n_v):
                    logger.warning(f"{k} at {lvl} don't pass space_insensitive_compare: '{o_v}' - '{n_v}'")
                    eq = False
            elif o_v != n_v:
                logger.warning(f"{k} at {lvl} don't match: '{o_v}' - '{n_v}'")
                eq = False
        return eq

    for key in [k for k in orig_keys if k not in ignore_for_level]:
        o = orig[key]
        n = new[key]

        if isinstance(o, dict) and isinstance(n, list):
            logger.info(f"{key} at {level} is dict in orig but list in new, making both list")
            o = [o]
        if isinstance(o, list) and isinstance(n, dict):
            n = [n]
            logger.info(f"{key} at {level} is dict in new but list in orig, making both list")

        if isinstance(o, list) and isinstance(n, list):
            if len(o) != len(n):
                logger.warning(f"{key} at {level} are lists of different length")
                are_equal = False
            elif key not in size_only:  # size check is enough
                for i in range(0, len(o)):
                    are_equal = compare_elements(are_equal, key, level, o[i], n[i]) and are_equal
        else:
            are_equal = compare_elements(are_equal, key, level, o, n) and are_equal

    return are_equal


def single_or_first(val):
    """
    this is to handle a specific case where ['val'] in 1 manifest but 'val' in other
    it's really only for thumbnail>service>profile
    """

    if isinstance(val, list):
        if len(val) > 1:
            raise ValueError(f"{val} has expected length of 1")
        return val[0]

    return val


def space_insensitive_compare(orig, new):
    # split into component parts
    orig_parts = orig.split("/")
    new_parts = new.split("/")

    # get any diffs, there should be 1 diff that is '5' (prod space)
    if diffs := list(set(orig_parts) - set(new_parts)):
        return len(diffs) == 1 and diffs[0] == '5'

    # if here, no diffs so they are teh same
    return True


async def run_comparison(bnumber):
    async with aiohttp.ClientSession(raise_for_status=True) as session:
        original = await load_manifest(session, bnumber, True)
        new = await load_manifest(session, bnumber, False)

        success = compare_manifests(bnumber, original, new)


async def load_manifest(session, bnumber, is_original):
    format = ORIGINAL_FORMAT if is_original else NEW_FORMAT
    uri = format.replace("{bnum}", bnumber)

    async with session.get(uri) as response:
        return await response.json()


# Press the green button in the gutter to run the script.
if __name__ == '__main__':
    asyncio.run(run_comparison('b19348216'))
