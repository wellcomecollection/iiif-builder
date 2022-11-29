var bigImage;
var authDo;
var assumeFullMax = false;
var viewer;

$(document).ready(function () {
    $(".row-toggle").click(function () { $(this).parents("tr").nextUntil(".row-overview").toggle(); });
    $(".metadata-toggle").click(function () { $(this).next("pre").toggle(); });
    $(".all-toggle").click(function () { $(".row-detail").toggle(); });
    $(".row-toggle").each(function() {
         if ($(this).parents("tr").nextUntil(".row-overview").size() === 0) {
             $(this).hide();
         }
    });
    $(".responseBody a").click(function () { $("#preview-responseBody").html($(this).next("pre").html()) });
    $("a.copyToClipboard").click(function(ev) {
        ev.preventDefault();
        $(this).after("<span>" + this.href + "</span>");
        var range = document.createRange();
        range.selectNode($(this).next()[0]);
        window.getSelection().addRange(range);
        try {
            document.execCommand('copy');
        } catch (err) {
        }
        window.getSelection().removeAllRanges();
        $(this).next().remove();
    });

    // Additions for previewing origins and images
    $('#authOps').hide();
    $('button.btn-prevnext').click(function () {
        var $tr = $('#' + $(this).attr('data-uri'));
        var $launcher = $tr.find('img.iiifpreview')[0];
        selectForModal($($launcher));
    });
    bigImage = $('#bigImage');
    bigImage.bind('error', function (e) {
        attemptAuth($(this).attr('data-uri'));
    });
    bigImage.bind('click', function (e) {
        launchOsd($(this).attr('data-uri') + "/info.json");
    });
    authDo = $('#authDo');
    authDo.bind('click', doClickthroughViaWindow);
    bindPreview();
    
    $("img.iiifpreview").unveil(300);
    
    var loc = window.location;
    var origin = loc.protocol + "//" + loc.hostname + (loc.port ? ':' + loc.port: '');
    $(".needsOrigin a").each(function(){
        if(this.search){
            this.href += "&origin=" + origin;
        } else {
            this.href += "?origin=" + origin;
        }
    });
});

// So that the toolbar doesn't hide links to named anchors.
var shiftWindow = function () { scrollBy(0, -60) };
if (location.hash) shiftWindow();
window.addEventListener("hashchange", shiftWindow);

$('#schForm').submit(function () {
    if ($.trim($("#schBox").val()) === "") {
        return false;
    }
});

$('.typeahead').typeahead({
    minLength: 4,
    highlight: true
},
{
    name: 'flat-manifs',
    source: getFlatManifestations,
    async: true,
    limit: 50,
    display: formatFlatManifestation

});

var gfmTimeout;

function getFlatManifestations(query, syncResults, asyncResults) {
    if (gfmTimeout) {
        clearTimeout(gfmTimeout);
    }
    gfmTimeout = setTimeout(function () {
        console.log('autocomplete - ' + query);
        $.ajax("/dash/AutoComplete/" + query).done(function (results) {
            asyncResults(results);
        });
    }, 300);
}

function formatFlatManifestation(fm) {
    return fm.id + " | " + fm.label;
}

// Viewer for manifestation page



function launchOsd(info) {
    // fetch the info.json ourselves so we can ignore HTTP errors from
    // clickthrough auth. This is a cheat for clickthrough.
    var $osdElement = $("#viewer");
    if (viewer) {
        viewer.destroy();
        viewer = null;
    }
    viewer = OpenSeadragon({
        element: $osdElement[0],
        prefixUrl: "openseadragon/images/"
    });
    viewer.addHandler("full-screen", function (ev) {
        if (!ev.fullScreen) {
            $osdElement.hide();
            bigImage.show();
        }
    });

    $osdElement.show();
    bigImage.hide();
    viewer.setFullScreen(true);

    doInfoAjax(info, loadTileSource);
}

function loadTileSource(jqXHR, textStatus) {
    var infoJson = $.parseJSON(jqXHR.responseText);
    // TODO - if we have an access token, fetch the 
    // info.json ourselves (with Authorisation header) and pass to OSD
    viewer.addTiledImage({
        tileSource: infoJson
    });
}


function bindPreview() {
    $('.iiifpreview').tooltip({
        
        html: true
    });
    $('.iiifpreview').click(function () {
        selectForModal($(this));
        $('#imgModal').modal();
    });
}

function selectForModal($launcher) {
    var iiif = $launcher.attr('data-iiif');
    $('.asset-table tr').removeClass('selected');
    var $tr = $launcher.closest('tr');
    var $trs = $tr.parent().children("tr");
    var trIndex = $tr.index();
    var trPrev = trIndex - 1;
    var trNext = trIndex + 1;
    $tr.addClass('selected');
    if (iiif) {
        var bigThumb = iiif + '/full/!1024,1024/0/default.jpg';
        bigImage.show();
        bigImage.attr('src', bigThumb); // may fail if auth
        bigImage.attr('data-src', bigThumb); // to preserve
        bigImage.attr('data-uri', iiif);
        $('#mdlLabel').text(iiif);
        if (trIndex > 0) {
            $('#mdlPrev').prop('disabled', false);
            $('#mdlPrev').attr('data-uri', $trs[trPrev].id);
        } else {
            $('#mdlPrev').prop('disabled', true);
        }
        if (trIndex < $trs.length - 1) {
            $('#mdlNext').prop('disabled', false);
            $('#mdlNext').attr('data-uri', $trs[trNext].id);
        } else {
            $('#mdlNext').prop('disabled', true);
        }
    }
}

function attemptAuth(imageService) {
    imageService += "/info.json";
    doInfoAjax(imageService, on_info_complete);
}

function reloadImage() {
    bigImage.show();
    bigImage.attr('src', bigImage.attr('data-src') + "#" + new Date().getTime());
}

function doInfoAjax(uri, callback, token) {
    var opts = {};
    opts.url = uri;
    opts.complete = callback;
    if (token) {
        opts.headers = { "Authorization": "Bearer " + token.accessToken }
        opts.tokenServiceUsed = token['@id'];
    }
    $.ajax(opts);
}

var infoJson;

function on_info_complete(jqXHR, textStatus) {

    infoJson = $.parseJSON(jqXHR.responseText);
    var services = getServices(infoJson);
    // leave out degraded for Wellcome for now

    if (jqXHR.status == 200) {
        // with the very simple clickthrough we shouldn't get back here unless there's a non-auth issue (eg 404, 500)
        // when this is reintroduced, need to handle the error on image - if it's not because of auth then reloading the image, 404, infinite loop.

        // reloadImage();
        // if (services.login && services.login.logout) {
        //     authDo.attr('data-login-or-out', services.login.logout.id);
        //     authDo.attr('data-token', services.login.token.id);
        //     changeAuthAction(services.login.logout.label);
        // }
        return;
    }

    if (jqXHR.status == 403) {
        alert('TODO... 403');
        return;
    }

    if (services.clickthrough) {
        bigImage.hide();
        authDo.attr('data-token', services.clickthrough.token.id);
        authDo.attr('data-uri', services.clickthrough.id);
        $('#authOps').show();
        $('.modal-footer').hide();
        $('#authOps h5').text(services.clickthrough.label);
        $('#authOps div').html(services.clickthrough.description);
        authDo.text(services.clickthrough.confirmLabel);
    }
    else {
        alert('only clickthrough supported from here');
    }
}

function doClickthroughViaWindow(ev) {

    var authSvc = $(this).attr('data-uri');
    var tokenSvc = $(this).attr('data-token');
    console.log("Opening click through service - " + authSvc + " - with token service " + tokenSvc);
    var win = window.open(authSvc); //
    var pollTimer = window.setInterval(function () {
        if (win.closed) {
            window.clearInterval(pollTimer);
            if (tokenSvc) {
                // on_authed(tokenSvc);
                $('#authOps').hide();
                $('.modal-footer').show();
                reloadImage(); // bypass token for now
            }
        }
    }, 500);
}


function getServices(info) {
    var svcInfo = {};
    var services;
    console.log("Looking for auth services");
    if (info.hasOwnProperty('service')) {
        if (info.service.hasOwnProperty('@context')) {
            services = [info.service];
        } else {
            // array of service
            services = info.service;
        }
        var prefix = 'http://iiif.io/api/auth/0/';
        var clickThrough = 'http://iiif.io/api/auth/0/login/clickthrough';
        for (var service, i = 0; (service = services[i]) ; i++) {
            var serviceName;

            if (service['profile'] == clickThrough) {
                serviceName = 'clickthrough';
                console.log("Found click through service");
                svcInfo[serviceName] = {
                    id: service['@id'],
                    label: service.label,
                    description: service.description,
                    confirmLabel: "Accept terms and Open" // fake this for now
                };
            }
            else if (service['profile'].indexOf(prefix) === 0) {
                serviceName = service['profile'].slice(prefix.length);
                console.log("Found " + serviceName + " auth service");
                svcInfo[serviceName] = { id: service['@id'], label: service.label };

            }
            if (service.service && serviceName) {
                for (var service2, j = 0; (service2 = service.service[j]) ; j++) {
                    var nestedServiceName = service2['profile'].slice(prefix.length);
                    console.log("Found nested " + nestedServiceName + " auth service");
                    svcInfo[serviceName][nestedServiceName] = { id: service2['@id'], label: service2.label };
                }
            }
        }
    }
    return svcInfo;
}


/**
 * jQuery Unveil
 * A very lightweight jQuery plugin to lazy load images
 * http://luis-almeida.github.com/unveil
 *
 * Licensed under the MIT license.
 * Copyright 2013 Lu�s Almeida
 * https://github.com/luis-almeida
 */

; (function ($) {

    $.fn.unveil = function (threshold, callback) {

        var $v = $(".viewer"), $w = $(window),
            th = threshold || 0,
            retina = window.devicePixelRatio > 1,
            attrib = retina ? "data-src-retina" : "data-src",
            images = this,
            loaded;

        this.one("unveil", function () {
            var source = this.getAttribute(attrib);
            source = source || this.getAttribute("data-src");
            if (source) {
                console.log("setting src " + source);
                this.setAttribute("src", source);
                if (typeof callback === "function") callback.call(this);
            }
        });

        function unveil() {
            var inview = images.filter(function () {
                var $e = $(this);
                // if ($e.is(":hidden")) return;

                var wt = $w.scrollTop(),
                    wb = wt + $w.height(),
                    et = $e.offset().top,
                    eb = et + $e.height();

                return eb >= wt - th && et <= wb + th;
            });

            loaded = inview.trigger("unveil");
            images = images.not(loaded);
        }

        $w.on("scroll.unveil resize.unveil lookup.unveil", unveil);
        $v.on("scroll.unveil resize.unveil lookup.unveil", unveil);

        unveil();

        return this;

    };

})(window.jQuery);

// Handle any non-functioning elements.
function handleIncomplete() {
    var elements = document.getElementsByClassName('wip');
    var len = elements.length;    
    for (var i = 0; i < len; i++){
        elements[i].addEventListener("click", function(evt){
            alert("This function is still to be migrated");
            evt.preventDefault();
        }, false);
    }
}

handleIncomplete();