
$(document).ready(function () {

    // Add "current" class to main navigation links
    if (location.pathname.length > 1 && location.pathname.indexOf('moh/') == -1) {
        $('.main-nav li a').each(function () {
            var navLinkTarget = $(this).attr('href');
            if (navLinkTarget == location.pathname ||
                (navLinkTarget != '/' && location.pathname.indexOf(navLinkTarget) == 0)) {
                $(this).addClass('current');
            }
        });
    } else if (window.location.hostname.indexOf('blog') == -1 && location.pathname.indexOf('moh/') == -1) {
        $('.main-nav li:first-child a').addClass("current");
    }

    // when the user clicks login, store the current url
    //$('.account-links .login').click(function () {
    //    jQuery.cookie('wlredirect', window.location.href, { path: '/' });
    //});

    //if (document.location.protocol.indexOf("https") == 0) {
    //    // avoid unnecessary https traffic
    //    $('a').each(function () {
    //        var newPrefix = window.libraryAuthRealm.replace("https:", "http:");
    //        newPrefix = newPrefix.substr(0, newPrefix.length - 1); // remove last "/"
    //        var tgtHref = $(this).attr('href');
    //        if (tgtHref.indexOf("/") == 0 && tgtHref.indexOf("//") != 0) {
    //            if (tgtHref.indexOf("/account") != 0
    //                && tgtHref.indexOf("/login") != 0
    //                && tgtHref.indexOf("/handlers/auth/Google") != 0)
    //                $(this).attr('href', newPrefix + tgtHref);
    //        }
    //    });
    //}


    // if user is logged in, change top nav links
    function authNav() {

        if (!window.libraryCasServer) return;
        if (window.isPatternPortfolio) return;


        if ($.cookie("wlauthssodisp")) {
            $.ajax({
                url: "/handlers/auth/checkSession.ashx",
                dataType: "jsonp",
                cache: false,
                success: function (response) {
                    console.log(response);
                    if (response === "OK") {
                        changeTopNavLinksForLogin($.cookie("wlauthssodisp"));
                    }
                }
            });
        }
        else {
            $('.site-title.reduced').before('<ul class="account-stores"></ul>');
        }
    }

    String.prototype.contains = function (it) { return this.indexOf(it) != -1; };

    //check if request is from outside to see if we should trigger a non-cached round trip to the server, to trigger a gateway op.
    //console.log("document.referrer: " + document.referrer);
    if (
        window.libraryCasServer && 
        (document.referrer.contains("catalogue.wellcomelibrary.org")
            || document.referrer.contains("search.wellcomelibrary.org")
            || !(document.referrer.contains("wellcomelibrary.org")))
        && !location.search.contains("ticketValidationFailure")
    ) {
        window.addEventListener("message", gatewayCallBack, false);
        var gateway = window.libraryCasServer + "?gateway=true&service=" + window.libraryAuthRealm + "casframe.aspx";
        $('#casFrame').attr('src', gateway);
    }

    authNav();

    function gatewayCallBack(event) {
        console.log("received postMessage");
        if (window.libraryAuthRealm.indexOf(event.origin) === 0 && event.message) {
            changeTopNavLinksForLogin(event.message);
        }
    }

    // tabbed body fields in Microsite
    $('div#tab2').hide();
    $('#tabList li a').click(function (e) {
        e.preventDefault();
        $('#tabList li a').removeClass('current');
        $('.hideable-tab').hide();
        $(this).addClass('current');
        $($(this).attr('href')).show();
        pushTrackEvent({
            category: "Site Interactions",
            action: "Tab Clicked",
            label: $(this).text()
        });
    });

    $('table.images-centered td').has('img').addClass('text-center');

//    // handle the search
//    $("#topSiteSearch").submit(function (e) {
//        // TODO: why no work?
//        //        if ($("#siteRadioButton:checked").length > 0) {
//        //            e.preventDefault();
//        //            var q = $("#searchQuery").val();
//        //            if (q) {
//        //                location.href = "/search/?q=" + q;
//        //            }
//        //        }
//    });

    // link tracking: http://www.blastam.com/blog/index.php/2013/03/how-to-track-downloads-in-google-analytics-v2/
    document.filetypes = /\.(zip|exe|dmg|pdf|doc.*|xls.*|ppt.*|mp3|txt|rar|wma|mov|avi|wmv|flv|wav)$/i;
    document.baseHref = '';
    if ($('base').attr('href') != undefined) document.baseHref = $('base').attr('href');

    $('a.track-link, .track-link a').on('click', function () {

        var el = $(this);
        var track = true;
        var href = (typeof (el.attr('href')) != 'undefined') ? el.attr('href') : "";
        var isThisDomain = href.match(document.domain.split('.').reverse()[1] + '.' + document.domain.split('.').reverse()[0]);
        if (!isThisDomain && href) {
            isThisDomain = (document.domain == href.replace(/^https?\:\/\//i, '').split('/')[0]);
        }
        if (!href.match(/^javascript:/i)) {
            var elEv = [];
            elEv.value = 0, elEv.non_i = false;
            elEv.category = "Links";
            if (href.match(/^mailto\:/i)) {
                elEv.action = "Mailto";
                elEv.label = href.replace(/^mailto\:/i, '');
                elEv.loc = href;
            } else if (href.match(document.filetypes)) {
                var extension = (/[.]/.exec(href)) ? /[^.]+$/.exec(href) : undefined;
                elEv.action = "Download " + extension[0];
                elEv.label = href.replace(/ /g, "-");
                elEv.loc = document.baseHref + href;
            } else if (href.match(/^https?\:/i) && !isThisDomain) {
                elEv.action = "External";
                elEv.label = href.replace(/^https?\:\/\//i, '');
                elEv.non_i = true;
                elEv.loc = href;
            } else if (href.match(/^tel\:/i)) {
                elEv.action = "Telephone";
                elEv.label = href.replace(/^tel\:/i, '');
                elEv.loc = href;
            } else track = false;

            if (track) {
                pushTrackEvent(elEv);
                if (el.attr('target') == undefined || el.attr('target').toLowerCase() != '_blank') {
                    setTimeout(function () { location.href = elEv.loc; }, 400);
                    return false;
                }
            }
        }
    });


    $('.download-as a').on('click', function () {
        var el = $(this);
        var track = false;
        var href = (typeof (el.attr('href')) != 'undefined') ? el.attr('href') : "";
        var elEv = [];
        elEv.value = 0, elEv.non_i = false;
        elEv.category = "Links";
        elEv.label = href.replace(/ /g, "-");
        elEv.loc = document.baseHref + href;
        if (href.indexOf('service/moh/zip') != -1) {
            elEv.action = "Download MoH Zip";
            track = true;
        }
        if (href.indexOf('service/moh/table') != -1) {
            elEv.action = "Download MoH Table";
            track = true;
        }
        if (track) {
            pushTrackEvent(elEv);
            // we don't need to do this becuase we're not unloading the page..
            //            if (el.attr('target') == undefined || el.attr('target').toLowerCase() != '_blank') {
            //                setTimeout(function () { location.href = elEv.loc; }, 400);
            //                return false;
            //            }
        }
    });

    $('#wlReducedLogo').on('click', function (e) {
        if (e.shiftKey || e.metaKey || e.ctrlKey) {
            e.preventDefault();
            var navBNumber = null;
            var otherPageUri, dataUri;
            if (location.pathname.indexOf('/plyr/b') === 0) {
                navBNumber = location.pathname.slice(8, 17);
                otherPageUri = '/item/' + navBNumber;
                dataUri = '/package/' + navBNumber;
            } else if (location.pathname.indexOf('/item/b') === 0) {
                navBNumber = location.pathname.slice(6, 15);
                otherPageUri = '/plyr/' + navBNumber;
                dataUri = '/iiif/' + navBNumber + '/manifest';
            }
            if (!navBNumber) return;
            
            if (e.shiftKey) {
                location.href = otherPageUri;
            }

            if (e.metaKey || e.ctrlKey) {
                location.href = dataUri;
            }
        }
    });

});

function pushTrackEvent(eventObj) {
    console.log("GA trackEvent: " + eventObj.category.toLowerCase() + ", " + eventObj.action.toLowerCase() + ", " + eventObj.label.toLowerCase() + ", " + eventObj.value + ", " + eventObj.non_i);
    if (typeof ga === 'undefined') {
        return;
    }
    ga('send', 'event', eventObj.category.toLowerCase(), eventObj.action.toLowerCase(), eventObj.label.toLowerCase(), eventObj.value || 0, eventObj.non_i);
    //_gaq.push(['_trackEvent', eventObj.category.toLowerCase(), eventObj.action.toLowerCase(), eventObj.label.toLowerCase(), eventObj.value || 0, eventObj.non_i]);
}

function isGuest() {
    var c = $.cookie("wlauthssodisp");
    if (!c) return false;
    var dispName = b64_to_utf8(c);
    var userTypeIndex = dispName.indexOf("|~|");
    return (dispName.substr(userTypeIndex + 3, 1) == 'G');
}

function changeTopNavLinksForLogin(base64DisplayName) {
    var dispName = b64_to_utf8(base64DisplayName);
    if (isGuest()) {
        //$('.account-links').prepend('<span class="user-name">' + dispName.substr(0, userTypeIndex) + '</span>'); //Remove this line after tests.
        $('.account-links').prepend('<span class="user-name"></span>');
        return false;
    }
    var userTypeIndex = dispName.indexOf("|~|");
    $('.account-links').empty();
    $('.account-links').append('<span class="user-name">' + dispName.substr(0, userTypeIndex) + '</span>');
    if (dispName.substr(userTypeIndex + 3, 1) == '.') {
        var patronId = dispName.substring(userTypeIndex + 4);
        var shortPatronId = patronId.slice(0, -1);
        $('.account-links').append('<a class="account button purple" href="' + window.encoreCatalogueSecureBaseUrl + '/patroninfo/' + shortPatronId + '/">My Library Account</a>');
    }
    var loginText = "My Bookmarks";
    $('.account-links').append('<a class="account button purple" href="' + window.libraryAuthRealm + 'account/">' + loginText + '</a>');
    $('.account-links').append('<a class="logout button" href="' + window.libraryAuthRealm + 'handlers/logout.ashx?returnUrl=' + location.pathname + '">Logout</a>');

    $('ul.account-stores').remove();
    //var ugcSettings = '<ul class="account-stores"><li><a href="#">Blah</a> <span>(812)</span></li><li><a href="#">Bookmarks</a> <span>(1)</span></li></ul>';
    var ugcSettings = '<ul class="account-stores"></ul>'; // TODO: make this dynamic later
    $('.site-title.reduced').before(ugcSettings);
}


function utf8_to_b64(str) {
    return window.btoa(unescape(encodeURIComponent(str)));
}

function b64_to_utf8(str) {
    return decodeURIComponent(escape(window.atob(str)));
}

function StringBuffer() {
    this.buffer = [];
}
StringBuffer.prototype.append = function append(string) {
    this.buffer.push(string);
    return this;
};
StringBuffer.prototype.toString = function toString() {
    return this.buffer.join("");
};