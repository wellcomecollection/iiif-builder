# IIIF2 Comparison

Python script for comparing 'new' Presentation 2 IIIF manifests vs existing wellcomelibrary.org versions.

This is _not_ a general purpose manifest comparer, instead it checks for known shape of IIIF data.

This is to ensure that the newly generate P2 manifests won't break any existing functionality.

## Implementation

The general process is to load both manifests for comparison. Then walk down the tree to compare branches and leaves. The following 2 functions do the bulk of the comparison work:

* `dictionary_comparison` takes 2 `dict` objects, representing original + new manifests. It compares the keys of each dictionary to validate they have the same keys. It then iterates through each key, calling `compare_elements` to compare individual values.
* `compare_elements` - takes individual elements from a `dict` (which may itself be another `dict`) and uses predefined rules to determine equality. It will compare simple objects, or use `dictionary_comparison` to compare `dicts`. 

In addition to the above the following 2 functions are used to do comparisons:

* `compare_services` - this compares the `"service"` element of a manifest. This is broken out as the number of values can differ between original + new so it is easier to handle these separately.
* `compare_embedded_manifests` - this walks through `"manifests"` element in a collection, fetches the respective manifests and compares them.

### Rules

A JSON element is used to define rules at each level. 

The main key in the dictionary is the "level" in the manifest. The sub-key is type of rule and their values are fields.

The rule types are (with the exception of `order_by` all values are `list`):

* `ignore` - No comparison is done on these properties but their existence is checked.
* `extra_new` - Properties that won't cause a failure if in the new manifests but not in the old.
* `extra_orig` - As above but extra property expected to be in original manifest.
* `version_insensitive` - Compare string values but ignore single values. This is for dlcs.io space or iiif.io profiles with different version numbers etc.
* `domain_insensitive` - Compare uri values but ignore domain.
* `bnumber_insensitive` - Compare strings, expecting only difference to be a bnumber.
* `dlcs_comparison` - Compare, coping for different dlc spaces and wc.org equivalents.
* `size_only` - don't check the actual values of list object, only verify that the size is the same.
* `order_by` - key to sort `[{}]` object prior to comparison.

E.g.

```py
rules = {
    "": {
        "ignore": ["@id", ],
    },
    "related": {
        "ignore": ["@id"],
    },
    "sequences-canvases": {
        "ignore": ["@id"]
    },
}
```

Would cause the following values to be ignored, however their existence would be checked.

```json
{
    "@id": "_ignored_",
    "related": {
        "@id": "_ignored_"
    },
    "sequences": {
        "canvases": [{
            "@id": "_ignored_",
        }]
    }
}
```