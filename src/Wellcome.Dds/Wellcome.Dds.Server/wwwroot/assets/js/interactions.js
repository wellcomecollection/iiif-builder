window.isPatternPortfolio = !(window.libraryAuthRealm);

// setup globals
var isPP = (document.querySelector('[data-is-pp]')) ? true : false;
var isLibrary = (document.querySelector('[data-library]')) ? true : false;
var isBlog = (document.querySelector('[data-blog]')) ? true : false;
var isCatalogue = (document.querySelector('[data-catalogue]')) ? true : false;


// tc temp
if (typeof console === "undefined" || typeof console.log === "undefined") {
    console = { };
    console.log = function() {
    };
}

/*!
 * jQuery Cookie Plugin
 * https://github.com/carhartl/jquery-cookie
 */
(function (g) {
    g.cookie = function (h, b, a) {
        if (1 < arguments.length && (!/Object/.test(Object.prototype.toString.call(b)) || null === b || void 0 === b)) {
            a = g.extend({}, a); if (null === b || void 0 === b) a.expires = -1; if ("number" === typeof a.expires) { var d = a.expires, c = a.expires = new Date; c.setDate(c.getDate() + d) } b = "" + b; return document.cookie = [encodeURIComponent(h), "=", a.raw ? b : encodeURIComponent(b), a.expires ? "; expires=" + a.expires.toUTCString() : "", a.path ? "; path=" + a.path : "", a.domain ? "; domain=" + a.domain : "", a.secure ? "; secure" :
            ""].join("")
        } for (var a = b || {}, d = a.raw ? function (a) { return a } : decodeURIComponent, c = document.cookie.split("; "), e = 0, f; f = c[e] && c[e].split("=") ; e++) if (d(f[0]) === h) return d(f[1] || ""); return null
    }
})(jQuery);


// Autogrow Textareas
(function($) {
    $.fn.autogrow = function(options) {

        this.filter('textarea').each(function() {

            var $this       = $(this),
                minHeight   = 72,
                lineHeight  = $this.css('lineHeight');

            var shadow = $('<div></div>').css({
                position:   'absolute',
                top:        -10000,
                left:       -10000,
                fontSize:   $this.css('fontSize'),
                fontFamily: $this.css('fontFamily'),
                lineHeight: $this.css('lineHeight'),
                resize:     'none'
            }).appendTo(document.body);

            var update = function() {

                var val = this.value.replace(/</g, '&lt;')
                                    .replace(/>/g, '&gt;')
                                    .replace(/&/g, '&amp;')
                                    .replace(/\n/g, '<br/>');

                shadow.css({width: $(this).width()}).html(val);
                $(this).css('height', Math.max(shadow.height() + 30, minHeight));
            }

            $(this).change(update).keyup(update).keydown(update);

            update.apply(this);

        });

        return this;

    }

})(jQuery);

function supports(prop) {
   var div = document.createElement('div'),
       vendors = ['Khtml','Ms','O','Moz','Webkit'],
       len = vendors.length;
   if (prop in div.style) return true;
   prop = prop.replace(/^[a-z]/, function(val) {
       return val.toUpperCase();
   });
   while(len--) {
       if (vendors[len] + prop in div.style) {
           return true;
       }
   }
   return false;
}

// Asyncronously load javascript
(function(d) {
    window.async = function(url) {
        var a = document.createElement('script');
        a.async = true;
        a.src = url;
        var b = d.getElementsByTagName('script')[0];
        b.parentNode.insertBefore(a, b);
    }
})(document);

// iOS orientationchange bug fix
(function(w){

    // This fix addresses an iOS bug, so return early if the UA claims it's something else.
    if( !( /iPhone|iPad|iPod/.test( navigator.platform ) && navigator.userAgent.indexOf( "AppleWebKit" ) > -1 ) ){
        return;
    }

    var doc = w.document;

    if( !doc.querySelector ){ return; }

    var meta = doc.querySelector( "meta[name=viewport]" ),
        initialContent = meta && meta.getAttribute( "content" ),
        disabledZoom = initialContent + ",maximum-scale=1",
        enabledZoom = initialContent + ",maximum-scale=10",
        enabled = true,
        x, y, z, aig;

    if( !meta ){ return; }

    function restoreZoom(){
        meta.setAttribute( "content", enabledZoom );
        enabled = true;
    }

    function disableZoom(){
        meta.setAttribute( "content", disabledZoom );
        enabled = false;
    }

    function checkTilt( e ){
        aig = e.accelerationIncludingGravity;
        x = Math.abs( aig.x );
        y = Math.abs( aig.y );
        z = Math.abs( aig.z );

        // If portrait orientation and in one of the danger zones
        if( !w.orientation && ( x > 7 || ( ( z > 6 && y < 8 || z < 8 && y > 6 ) && x > 5 ) ) ){
            if( enabled ){
                disableZoom();
            }
        }
        else if( !enabled ){
            restoreZoom();
        }
    }

    w.addEventListener( "orientationchange", restoreZoom, false );
    w.addEventListener( "devicemotion", checkTilt, false );

})(this);

// Flexbox Support Detection
(function() {
    document.getElementsByTagName('html')[0].className += (supports('flex-box') || supports('box-flex'))? ' flexbox' : '';
})();

// Relative Dates
(function(win){
    win.relativeDate = function(from) {
        from = new Date(from);
        var to = new Date;
        var seconds = ((to - from) / 1000);
        var minutes = Math.floor(seconds / 60);
        if (minutes == 0) { return 'less than a minute ago'; }
        if (minutes == 1) { return 'a minute ago'; }
        if (minutes < 45) { return minutes + ' minutes ago'; }
        if (minutes < 90) { return 'an hour ago'; }
        if (minutes < 1440) { return Math.floor(minutes / 60) + ' hours ago'; }
        if (minutes < 2880) { return '1 day ago'; }
        if (minutes < 43200) { return Math.floor(minutes / 1440) + ' days ago'; }
        if (minutes < 86400) { return '1 month ago'; }
        if (minutes < 525960) { return Math.floor(minutes / 43200) + ' months ago'; }
        if (minutes < 1051199) { return '1 year ago'; }
        return Math.floor(minutes / 525960) + ' years ago';
    }
})(this);

// Responsive Selectors
(function (e) {
    function q() { if (p) { var b = []; if (f.querySelectorAll) b = f.querySelectorAll("[data-squery]"); else for (var a = f.getElementsByTagName("*"), c = 0, m = a.length; c < m; ++c) a[c].getAttribute("data-squery") && b.push(a[c]); c = 0; for (m = b.length; c < m; ++c) { for (var a = b[c], d = [], e = a.getAttribute("data-squery").split(" "), g = 0, i = e.length; g < i; ++g) { var h = /(.*):([0-9]*)(px|em)=(.*)/.exec(e[g]); h && d.push(h) } a.cq_rules = a.cq_rules || []; a.cq_rules = a.cq_rules.concat(d); j.push(a) } } } function k() {
        for (var b = 0, a = j.length; b < a; ++b) {
            el =
            j[b]; for (var c = 0, e = el.cq_rules.length; c < e; ++c) {
                var d = el.cq_rules[c], f = parseInt(d[2]); "em" === d[3] && (f = n(parseFloat(d[2]), el)); var g = el, i = d[4], h = g.cloneNode(!0); h.className = (" " + h.className + " ").replace(" " + i + " ", " "); h.style.height = 0; h.style.visibility = "none"; h.style.overflow = "hidden"; h.style.clear = "both"; i = g.parentNode; i.insertBefore(h, g); g = h.offsetWidth; i.removeChild(h); r[d[1]](g, f) ? 0 > el.className.indexOf(d[4]) && (el.className += " " + d[4]) : (d = el.className.replace(RegExp("(^| )" + d[4] + "( |$)"), "$1"),
                d = d.replace(/ $/, ""), el.className = d)
            }
        }
    } function l() { if (!o) { o = !0; q(); k(); e.addEventListener && e.addEventListener("resize", k, !1); var b = n(1, f.body); e.setInterval(function () { var a = n(1, f.body); a !== b && (k(), b = a) }, 100) } } var f = e.document, j = [], p = !0, o = !1, r = { "min-width": function (b, a) { return b > a }, "max-width": function (b, a) { return b < a } }, n = function (b) { return function () { var a = Array.prototype.slice.call(arguments); b.memoize = b.memoize || {}; return a in b.memoize ? b.memoize[a] : b.memoize[a] = b.apply(this, a) } }(function (b, a) {
        var c =
        f.createElement("div"); c.style.fontSize = "1em"; c.style.margin = "0"; c.style.padding = "0"; c.style.border = "none"; c.style.width = "1em"; a.appendChild(c); var e = c.offsetWidth; a.removeChild(c); return Math.round(e * b)
    }); f.addEventListener ? (f.addEventListener("DOMContentLoaded", l, !1), e.addEventListener("load", l, !1)) : f.attachEvent && (f.attachEvent("onreadystatechange", l), e.attachEvent("onload", l)); e.SelectorQueries = {
        add: function (b, a, c, e) {
            for (var c = /([0-9]*)(px|em)/.exec(c), d = 0, f = b.length; d < f; ++d) {
                var g = b[d]; g.cq_rules =
                g.cq_rules || []; g.cq_rules.push([null, a, c[1], c[2], e]); j.push(g)
            } o && k()
        }, ignoreDataAttributes: function () { p = !1 }
    }
})(this);

// Check if we should load high resolution images
(function($) {

    $.fn.responsiveImages = function(width, monitor) {
        this.each(function() {
            $(this).bind('load', function() {
                $(this).responsiveImages(width, false);
            });
            if (!$(this).hasClass('large') && $(this).width() > width) {
                this.className += ' large';
                var src = this.src;
                var ext;
                if (window.isPatternPortfolio) {
                    ext = src.substr(src.lastIndexOf('.'));
                }
                var img = new Image();
                $(img).on('load', { target: this }, function(e) {
                    //window.resizedLog[this.src] = true; // hack
                    e.data.target.src = this.src;
                });

                if (window.isPatternPortfolio) {
                    // OLD:
                    img.src = src.replace(ext, '-large' + ext);
                } else {
                    // NEW:
                    //  * the editor specifies /real/path/to/image.jpg as the carousel image
                    //  * the template emits the image URL like so:
                    //      src="/resize/width/height/cacheSeconds/real/path/to/image.jpg"
                    //      e.g.,            0 indicates scale to fit; width or height can be 0 but not both
                    //      src="/resize/300/0/1800/content/hero-images/home-page-carousel/wellcome-library.jpg
                    //  * the above gives you wellcome-library.jpg scaled to 300px wide
                    // so the replacement should restore the img src to the original image the editor chose
                    // src might start with "http://blahblah/resize..."
                    img.src = removeResize(src);
                }
            }
        });
        if (monitor != false) {
            var el = this;
            $(window).bind('resize', function() {
                el.responsiveImages(width, false);
            });
        }
    };
})(jQuery);

function removeResize(existingSrc) {
    // console.log("removeResize called for " + existingSrc);
    if (existingSrc.indexOf('/resize/') >= 0) {
        //return '/content/hero-images/home-page-carousel/wellcome-library.jpg';
        return '/' + existingSrc.substring(existingSrc.indexOf('/resize/')).split('/').slice(5).join('/');
    }
    return existingSrc;
}

// Throttle Function Calls
function throttle(fn, delay) {
  var timer = null;
  return function () {
    var context = this, args = arguments;
    clearTimeout(timer);
    timer = setTimeout(function () {
      fn.apply(context, args);
    }, delay);
  };
}

// Responsive Section Navigation
// Inserts toggle links for revealing section navigation on small screens
(function($) {
    if ($('.section-nav').length > 0 || $('.search-option-tabs').length > 0) {
        if ($('.section-nav').length > 0) {
            var $children = $('.section-nav .current').siblings('ul');
            $children = $children.clone().addClass('children-nav');
        } else {
            var $children = $('.search-option-tabs');

        }
        // Are there any children of the current page?
        if ($children.length > 0) {
            if ($('.section-nav').length > 0) {
                $('.section-nav').before($children);
            }
            var $title = $('.page-title');
            $title.after('<a href="#" class="children-nav-toggle"><span>Show Navigation</span></a>');
            $('.children-nav-toggle').on('click', function(e) {
                $(this).blur();
                $(this).toggleClass('active');
                $children.toggleClass('show');
                e.preventDefault();
            });
        }
    }
})(jQuery);

// Responsive Columns
// Ensures columns never get too small
(function($) {
    if ('__proto__' in {} && $('.section.search-section').length == 0) {
        var $columns = $('.group .column');
        // If columns are less than 16em wide, apply the class 'full'
        SelectorQueries.add($columns, "max-width", "15.9em", "full");
        SelectorQueries.ignoreDataAttributes();
    }
})(jQuery);

// Cross browser nth-child fallback for columns
(function($) {
    var $columns = $('.group .column');
    // If nth child isn't supported by the browser
    if ($columns.length > 0 && $columns.eq(0).css('clear') != 'left') {
        $('.group.half .column:nth-child(2n+1), .group.third .column:nth-child(3n+1)').addClass('clear');
        $('.lightbox-assets li:nth-child(4n+1)').addClass('clear');
    }
})(jQuery);



// Main nav
(function($) {
    // track links
    var $mainNav = $('.main-nav');
    if ($mainNav.length) {
        $mainNav.find('a').on('click', function(e) {
            pushTrackEvent({
                category: "Site Interactions",
                action: "Top level navigation",
                label: document.URL + ", " + $(this).attr('href')
            });
        });
    }
})(jQuery);

// Carousel Interactions
(function(win, $) {
    if ($('.carousel').length > 0) {
        var $carousel = $('.carousel'),
            $slide = $('a.slide', $carousel),
            $img = $('img', $carousel),
            large = false,
            fixedHeight = false,
            winWidth = 0;

        // track links
        $slide.find('.action').on('click', function(e) {
            pushTrackEvent({
                category: "Site Interactions",
                action: "Accordion link",
                label: document.URL + ", " + $(this).closest('a').attr('href')
            });
        });

        // Removes inline width and height from images
        function removeCarouselHeight() {
            if (fixedHeight == true && winWidth != $(win).width()) {
                $img.each(function() {
                    $(this).css({width:'', height:''});
                });
                fixedHeight = false;
            }
        }
        // Give images an explicit height based on their inherited widths
        function setCarouselHeight() {
            setTimeout(function() {
                if (winWidth != $(win).width()) {
                    removeCarouselHeight();
                    winWidth = $(win).width();
                    if ($img.parent('a').css('overflow') == 'hidden') {
                        var h = $img.eq(0).height();
                        $img.each(function() {
                            $(this).css({width: 'auto', height: h+1});
                        });
                        $img.eq(0).on('load', function(e) {
                            winWidth = 0;
                            fixedHeight = true;
                            removeCarouselHeight();
                            setCarouselHeight();
                        });
                    }
                }
            }, 10);
        }
        $(win).on('resize', function(e) {
            removeCarouselHeight();
        }).on('resize', throttle(function(e) {
            setCarouselHeight();
        }, 200));
        setCarouselHeight();
        $img.responsiveImages(400);
        // Hover and click functionality
        $carousel.on('click', 'a.slide', function(e) {
            $(this).blur();
            if (!$(this).hasClass('active')) {
                $carousel.addClass('transitioning');
                $('a.slide.active', $carousel).removeClass('active');
                $(this).addClass('active');
                setTimeout(function() {
                    $carousel.removeClass('transitioning');
                }, 500);
                updateNavLinks($(this));
                updatezIndex($(this));
                e.preventDefault();

                pushTrackEvent({
                    category: "Site Interactions",
                    action: "Accordion click",
                    label: document.URL + ", " + $(this).find('img').attr('src')
                });
            }
        }).on('touchstart', 'a.slide', function(e) {}).append('<ul class="carousel-nav"><li><a href="#" class="prev inactive">Previous</a></li><li><a href="#" class="next">Next</a></li></ul>');
        var $prev = $('.carousel-nav a.prev', $carousel),
            $next = $('.carousel-nav a.next', $carousel);
        $('.carousel-nav', $carousel).on('click', 'a', function(e) {
            $(this).blur();
            var $active = $('a.slide.active', $carousel);
            if ($(this).hasClass('prev')) {
                var $new = $active.prev('a.slide');
            } else {
                var $new = $active.next('a.slide');
            }
            if ($new.length > 0) {
                $new.addClass('active top').css({opacity: 0}).animate({opacity: 1}, 300, function() {
                    $active.removeClass('active');
                    $new.removeClass('top');
                });
                updateNavLinks($new);
                updatezIndex($new);
            }
            e.preventDefault();
        });
        function updateNavLinks($active) {
            if ($active.prev('a.slide').length == 0) {
                $prev.addClass('inactive');
            } else {
                $prev.removeClass('inactive');
                if ($active.next('a.slide').length == 0) {
                    $next.addClass('inactive');
                } else {
                    $next.removeClass('inactive');
                }
            }
        }
        function updatezIndex($active) {
            var z = 100;
            $slide = $active;
            while ($slide.length > 0) {
                $slide.css({zIndex: z});
                z = z - 10;
                $slide = $slide.next('a.slide');
            }
            $slide = $active.prev('a.slide');
            while ($slide.length > 0) {
                $slide.css({zIndex: z});
                z = z - 10;
                $slide = $slide.prev('a.slide');
            }
        }
    }
})(this, jQuery);

// Responsive Carousel
(function(win, $) {
    if ($('.subject-carousel').length > 0) {
        var $carousel = $('.subject-carousel'),
            $slides = $('.slide', $carousel);
        if ($slides.length > 1) {
            var $active = $('.slide.active', $carousel);
            $carousel.addClass('with-nav').prepend('<a class="prev disabled" href="#">Previous</a><a class="next" href="#">Next</a>');
            var $nav = $('a.prev, a.next', $carousel),
                $prev = $('a.prev', $carousel),
                $next = $('a.next', $carousel);
            $nav.height($carousel.height()-40);
            $('img', $carousel).on('load', function() {
                setNavHeight();
            });
            $(win).on('resize', function() {
                setNavHeight();
            });
            function setNavHeight() {
                $nav.height($active.height()-20);
            }
            setNavHeight();
            $prev.on('click', function(e) {
                if (!$(this).hasClass('disabled')) {
                    var $slide = $active.prev('.slide');
                    checkDisabled($slide);
                    $slide.css({right: $active.width(), width: $active.width(), height: $active.height(), display: 'block'}).animate({right: 46}, 300, function() {
                        $slide.addClass('active').removeAttr('style');
                        $active.removeClass('active');
                        $active = $slide;
                        setNavHeight();
                    });

                    pushTrackEvent({
                        category: "Site Interactions",
                        action: "Carousel click",
                        label: "Left"
                    });
                }
                e.preventDefault();
            });
            $next.on('click', function(e) {
                if (!$(this).hasClass('disabled')) {
                    var $slide = $active.next('.slide');
                    checkDisabled($slide);
                    $slide.css({left: $active.width(), width: $active.width(), height: $active.height(), display: 'block'}).animate({left: 46}, 300, function() {
                        $slide.addClass('active').removeAttr('style');
                        $active.removeClass('active');
                        $active = $slide;
                        setNavHeight();
                    });

                    pushTrackEvent({
                        category: "Site Interactions",
                        action: "Carousel click",
                        label: "Right"
                    });
                }
                e.preventDefault();
            });
            function checkDisabled($slide) {
                if ($slide.prev('.slide').length == 0) {
                    $prev.addClass('disabled');
                } else {
                    $prev.removeClass('disabled')
                }
                if ($slide.next('.slide').length == 0) {
                    $next.addClass('disabled');
                } else {
                    $next.removeClass('disabled')
                }
            }
        }
    }
})(this, jQuery);

// Responsive Section Image
(function($) {
    $('.section-image').responsiveImages(460);
})(jQuery);

// Social Links Interactions
(function($) {
    if ($('.social-links').length > 0) {
        $('.section.footer').append('<div id="fb-root"></div>');
        async('//connect.facebook.net/en_GB/all.js#xfbml=1');
        async('//platform.twitter.com/widgets.js');
        window.addthis_config = {
            ui_click: true,
            ui_open_windows: true
        };
        window.addthis_share = {
            templates: {
               twitter: '{{title}}: {{url}}'
            }
        };
        async('//s7.addthis.com/js/250/addthis_widget.js#pubid=ra-4f5f35981d6a489d');
    }
})(jQuery);

// Show Latest Tweets
(function($) {
    function standardiseDate(string) {
        var s = string.split(' ');
        string = s[0]+', '+s[2]+' '+s[1]+' '+s[5]+' '+s[3]+' GMT'+s[4];
        return string;
    }
    if ($('.latest-tweets').length > 0) {
        var $latestTweets = $('.latest-tweets');
        var request = $.ajax({
            url: '/handlers/twitterfeed.ashx',
            dataType: 'json'
        });
        request.done(function(tweets) {
            var len = tweets.length,
                html = '',
                links = '';
            for (var i=0; tweet=tweets[i], i<len; i++) {

                tweet.created_at = standardiseDate(tweet.created_at);
                //Build Tweet HTML
                if (tweet.entities) {
                    // Link URLs
                    if (tweet.entities.urls) {
                        for (var j=0, elen=tweet.entities.urls.length; entity=tweet.entities.urls[j], j<elen; j++) {
                            tweet.text = tweet.text.replace(entity.url, '<a href="'+entity.expanded_url+'">'+entity.display_url+'</a>');
                        }
                    }
                    // Link Users
                    if (tweet.entities.user_mentions) {
                        for (var j=0, elen=tweet.entities.user_mentions.length; entity=tweet.entities.user_mentions[j], j<elen; j++) {
                            tweet.text = tweet.text.replace(new RegExp('(^| )@'+entity.screen_name, 'gm'), '$1<a href="http://twitter.com/'+entity.screen_name+'" title="View '+entity.name+'&rsquo;s profile on Twitter">@'+entity.screen_name+'</a>');
                        }
                    }
                    // Link Hashtags
                    if (tweet.entities.hashtags) {
                        for (var j=0, elen=tweet.entities.hashtags.length; entity=tweet.entities.hashtags[j], j<elen; j++) {
                            tweet.text = tweet.text.replace(new RegExp('(^| )#'+entity.text, 'gm'), '$1<a href="https://twitter.com/hashtag/' +entity.text+'" class="muted" title="View this hashtag on Twitter">#'+entity.text+'</a>');
                        }
                    }
                }
                html += '<div id="tweet-'+tweet.id_str+'" class="tweet';
                if (i==0) {
                    html += ' active'
                }
                html += '">'+"\n";
                html += '<blockquote><p>'+tweet.text+"</p></blockquote>\n";
                html += '<p class="date">'+relativeDate(tweet.created_at)+"</p>\n";
                html += "</div>\n";
                links += '<li><a href="#" data-for="tweet-'+tweet.id_str+'"';
                if (i==0) {
                    links += ' class="active"';
                }
                links += '><span>Show Tweet '+i+"</span></a></li>\n";
            }
            html += '<ul class="tweet-links">'+"\n"+links+'</ul>';
            $latestTweets.html(html).addClass('show');
            var nextTweet = setInterval(function() {
                var tweet = $('.tweet.active', $latestTweets).next('.tweet');
                if (tweet.length == 0) {
                    tweet = $('.tweet', $latestTweets).eq(0);
                }
                changeTweet(tweet.attr('id'));
            }, 8000);
            $('.tweet-links a', $latestTweets).on('click', function(e) {
                clearInterval(nextTweet);
                changeTweet($(this).attr('data-for'));
                $(this).blur();
                e.preventDefault();
            });
        });
        request.fail(function() {
            $latestTweets.addClass('show');
        });
        function changeTweet(id) {
            $('.tweet-links .active', $latestTweets).removeClass('active');
            $('.tweet-links [data-for='+id+']', $latestTweets).addClass('active');
            $('.tweet.active', $latestTweets).fadeOut(300, function() {
                $(this).removeClass('active');
                $('.tweet#'+id, $latestTweets).fadeIn(300, function() {
                    $(this).addClass('active');
                });
            });
        }
    }
})(jQuery);

// Artifact Information Toggle
(function($) {
    if ($('.artifact .information').length > 0) {
        var $artifact = $('.artifact'),
            $information = $('.information', $artifact);
        $artifact.append('<a class="reveal" href="#"><span>Show information</span></a>');
        $information.append('<a class="close" href="#">Close</a>');
        var $reveal = $('.reveal', $artifact),
            $close = $('.close', $information);
        $reveal.on('click', function(e) {
            $information.show(300);
            $(this).fadeOut(300);
            e.preventDefault();
        });
        $close.on('click', function(e) {
            $information.hide(300);
            $reveal.fadeIn(300);
            e.preventDefault();
        });
    }
})(jQuery);

// Login Page
//(function() {
//    if($('#username').val()) {
//        $('.bookmarks-form').show();
//    } else {
//        var $bookmarksReveal = $('.reveal-bookmarks');
//        if ($bookmarksReveal.length > 0) {
//            $bookmarksReveal.on('click', function(e) {
//                $('.bookmarks-form').slideDown(300, function() {
//                    $('.bookmarks-form input').eq(0).focus();
//                });
//                e.preventDefault();
//            });
//        }
//    }
//})();

// Search Tool Finder Interactions
(function($) {
    if ($('.search-finder').length > 0) {
        function hasLayout() {
            return ($descriptions.css('position') == 'absolute');
        }
        function setMaxHeight() {
            $heightSelectors.css({height:''});
            var maxHeight = Math.max($searchFinder.height(),
                $descriptions.has('.active').height());
            if (hasLayout()) {
                $heightSelectors.height(maxHeight);
            }
        }
        var $searchFinder = $('.search-finder'),
            $descriptions = $('dd', $searchFinder),
            $heightSelectors = $searchFinder.add($descriptions);
        setMaxHeight();
        $searchFinder.on('click', 'dt', function(e) {
            $(this).blur();
            if ($(this).hasClass('active')) {
                return;
            }
            var $dt = $(this),
                $dd = $dt.next('dd'),
                $dtActive = $('dt.active', $searchFinder),
                $ddActive = $('dd.active', $searchFinder);
            $dtActive.removeClass('active');
            $dt.addClass('active');
            if (hasLayout()) {
                var $activeInner = $('div', $ddActive);
                $activeInner.fadeOut(500, function() {
                    $dd.css({zIndex: 10}).fadeIn(500, function() {
                        $(this).addClass('active').removeAttr('style');
                        $activeInner.removeAttr('style');
                        $ddActive.removeClass('active');
                        setMaxHeight();
                    });
                });
            } else {
                $ddActive.slideUp(500, function() {
                    $(this).removeClass('active').removeAttr('style');
                });
                $dd.slideDown(500, function() {
                    $(this).addClass('active').removeAttr('style');
                });
            }
        });
        $(window).on('resize', function(e) {
            setMaxHeight();
        });
    }
})(jQuery);

// Image Gallery Interactions
(function($) {
    if ($('.image-gallery').length > 0) {
        $('body').prepend('<div class="gallery-overlay"></div>');
        var $overlay = $('.gallery-overlay');
        function initGallery(el, maximised) {
            var $gallery = $(el),
                $slides = $('.slide', el),
                $actions = $('.gallery-images', el),
                $thumbList = $('ul', $actions),
                $prev = $('a.prev', $actions),
                $next = $('a.next', $actions),
                $thumbs = $('li a', $actions),
                slideNum = 0,
                last = $slides.length - 1,
                $overlayGallery;
            $actions.on('click', 'a', function(e) {
                if ($(this).hasClass('prev')) {
                    if (slideNum != 0) {
                        switchImage(--slideNum);
                    }
                } else if ($(this).hasClass('next')) {
                    if (slideNum != last) {
                        switchImage(++slideNum);
                    }
                } else {
                    slideNum = parseInt($thumbs.index(this));
                    switchImage(slideNum);
                }
                e.preventDefault();
            });
            function switchImage(slideNum) {
                var $slide = $slides.eq(slideNum);
                if (!$slide.hasClass('active')) {
                    $slides.filter('.active').removeClass('active');
                    $slide.addClass('active');
                    $('.active', $actions).removeClass('active');
                    var $thumb = $thumbs.eq(slideNum);
                    $thumb.addClass('active');
                    thumbListScroll = $thumbList.scrollLeft();
                    thumbListWidth = $thumbList.width();
                    // console.log(thumbListScroll);
                    thumbPosition = $thumb.position().left;
                    thumbWidth = $thumb.width();
                    // console.log(thumbPosition);
                    if (thumbPosition < 34) {
                        var newPosition = thumbListScroll+thumbPosition-34;
                        // console.log(newPosition);
                        $thumbList.scrollLeft(newPosition);
                    } else if (thumbPosition + thumbWidth > thumbListWidth) {
                        $thumbList.scrollLeft(thumbListScroll + thumbWidth);
                    }
                    if (slideNum == 0) {
                        $prev.addClass('disabled');
                    } else {
                        $prev.removeClass('disabled');
                    }
                    if (slideNum == last) {
                        $next.addClass('disabled');
                    } else {
                        $next.removeClass('disabled');
                    }
                    if (maximised) {
                        $overlayGallery = $('.image-gallery', $overlay);
                        centerGallery($overlayGallery);
                    }
                }
            }
            $gallery.on('click', function(e) {
                e.stopPropagation();
            });
            $overlay.add('.close', $overlay).on('click', function(e) {
                removeOverlay();
                e.preventDefault();
            });
            $slides.on('click', 'img', function(e) {
                if (maximised) {
                    removeOverlay();
                } else {
                    $overlay.html($gallery.clone());
                    $overlayGallery = $('.image-gallery', $overlay);
                    $('.slide img', $overlayGallery).each(function() {
                        /*
                        var src = this.src,
                            ext = src.substr(src.lastIndexOf('.')),
                            src = src.replace(ext, '-large'+ext);
                        this.src = src;*/
                        this.src = removeResize(this.src);
			$(this).on('load', function() {
                            centerGallery($overlayGallery);
                        });
                    });
                    $overlayGallery.prepend('<a class="close" href="#">Close</a>');
                    initGallery($overlayGallery, true);
                    $overlay.css({opacity: 0, display: 'block'});
                    $overlay.animate({opacity: 1}, 300);
                    centerGallery($overlayGallery);
                }
                e.preventDefault();
            });
            function centerGallery($overlayGallery) {
                var top = Math.max(10, $(window).height()/2 - $overlayGallery.height()/2);
                $overlayGallery.css('margin-top', top);
            }
            function removeOverlay() {
                $overlay.fadeOut(300, function() {
                    $('.image-gallery', $overlay).remove();
                });
            }
        }
        $('.image-gallery').each(function() {
            initGallery(this, false);
        });
    }
})(jQuery);

// Global navigation for small screens
(function($) {
    var $html = $('html'),
        $header = $('.section.header'),
        $pageWrap = $('.page-wrap');
    var supportsTransition = supports('transition'),
        ua = navigator.userAgent.toLowerCase(),
        isAndroid = ua.indexOf('android'),
        has3d = (supports('perspective') && !( ($.browser.mozilla && $.browser.version < 13) || (isAndroid !== -1 && parseFloat(ua.slice(isAndroid+8,isAndroid+9)) < 3) ) );
    if (has3d) {
        $('html').addClass('has3d');
    }
    var globalMenu = '<div class="global-menu">';
    if ($('.account-links', $header).length > 0) {
        globalMenu += '<div class="account-links">'+$('.account-links', $header).html()+'</div>';
    }
    var mainNav = $('.main-nav', $header).html();
    if(mainNav) {
        globalMenu += '<ul class="main-nav">'+ mainNav + '</ul>';
    }
    var searchUi = $('.search', $header).html();
    if(searchUi) {
        globalMenu += '<form class="search" method="get" action="'+$('.search', $header).attr('action')+'">' + searchUi + '</form>';
    }
    globalMenu += '<ul class="secondary-nav">'+$('.footer-nav').html()+'</ul>';
    var utLinks = $('.ut .ut-links').html();
    if(utLinks) {
        globalMenu += '<ul class="ut-links">'+ utLinks + '</ul>';
    }
    globalMenu += '</div>';
    $pageWrap.before(globalMenu);
    $('.site-title', $header).before('<a href="#" class="toggle-global-menu"><span></span>Menu</a>');
    var $globalMenu = $('.global-menu');
    var $toggleGlobalMenu = $('.toggle-global-menu', $header);
    $toggleGlobalMenu.on('click', function(e) {
        if ($html.hasClass('reveal-menu')) {
            hideGlobalMenu(500);
        } else {
            showGlobalMenu();
        }
        e.preventDefault();
    });
    function showGlobalMenu() {
        $html.addClass('reveal-menu');
        var width = $(window).width()-50;
        $globalMenu.width(width);
        if (has3d) {
            setTransform('translate3d('+width+'px,0,0)');
        } else {
            $pageWrap.css({marginLeft: width, marginRight: -width});
        }
        $(window).on('touchend scroll', globalTouchEnd).on('resize', globalResize);
    }
    function hideGlobalMenu(animate) {
        if (animate && supportsTransition) {
            setTimeout(function() {
                $html.removeClass('reveal-menu');
            }, animate);
        } else {
            $html.removeClass('reveal-menu');
        }
        if (has3d) {
            setTransform('translate3d(0,0,0)');
        } else {
            $pageWrap.css({marginLeft: 0, marginRight: 0});
        }
        $(window).off('touchend scroll', globalTouchEnd).off('resize', globalResize);
    }
    var globalTouchEnd = throttle(function() {
        if ($(window).scrollLeft() > 20) {
            hideGlobalMenu(false);
        }
    }, 50);
    var globalResize = throttle(function() {
        if ($toggleGlobalMenu.css('display') != 'block') {
            hideGlobalMenu(false);
        }
    }, 50);
    function setTransform(transform) {
        $pageWrap.css({'-webkit-transform': transform, '-moz-transform': transform, '-ms-transform': transform, '-o-transform': transform, 'transform': transform});
    }
})(jQuery);

// Sitemap
(function($) {
    if ($('.sitemap-list').length > 0) {
        var $sitemap = $('.sitemap-list');
        $('ul ul', $sitemap).each(function() {
            $(this).before('<a href="#" class="more">More</a>').hide();
        });
        $sitemap.on('click', '.more', function(e) {
            $(this).next('ul').slideDown(300);
            $(this).slideUp(300, function() {
                $(this).remove();
            });
            e.preventDefault();
        });
    }
})(jQuery);

// Search By Subject
(function($) {
    if ($('.subject-search-results').length > 0) {
        $('.subject-search-results').on('click', '.related-results-toggle', function() {
            $target = $('.related-results', $(this).parents('li'));
            if ($(this).hasClass('close')) {
                $target.slideUp(300);
                $(this).removeClass('close');
            } else {
                $target.slideDown(300);
                $(this).addClass('close');
            }
        });
    }
})(jQuery);

// Blog Comments
(function($) {
    if ($('#commentform').length > 0) {
        var $commentForm = $('#commentform, #reply-title');
        $('#reply-title').before('<a href="#" class="show-commentform button purple">Leave a Reply</a>');
        $('.show-commentform').on('click', function(e) {
            $(this).remove();
            $commentForm.slideDown(300);
            e.preventDefault();
        });
    }
})(jQuery);

// Print link
(function($) {
    $('.print-link').on('click', function(e) {
        window.print();
        e.preventDefault();
    });
})(jQuery);

(function($) {
    if (location.pathname.indexOf("/player") == 0 || location.pathname.indexOf("/oldplayer") == 0) {
        $("#contactUsLink").hide();
    }

    // cookie consent temporarily disabled on wellcomelibrary.org
    if ($.cookie('cookieconsent_lib') != 'agreed') // && window.location.hostname.indexOf('wellcomelibrary.org') == -1)
    {
        $('#cookieconsent').css('display', 'block');
    }

    // temp -make this global...
    var consentCookieOptions = { expires: 9999, path: '/' };
    // please leave the above line intact, exactly as is. A Regex replaces it with the following for live environments:
    //var consentCookieOptions = { expires: 9999, path: '/', domain: '.wellcome.ac.uk' };

    // and then we have this temporary thing to accomodate multiple hostnames:
    if (consentCookieOptions.domain == '.wellcome.ac.uk' && window.location.hostname.indexOf('wellcomelibrary.org') != -1) {
        consentCookieOptions.domain = ".wellcomelibrary.org";
    }

    $('#accept').click(function() {
        $.cookie('cookieconsent_lib', 'agreed', consentCookieOptions);
        $('#cookieconsent').css('display', 'none');
    });

    // alert popups
    var popupcookie = $.cookie('alertpopups_lib') || '';
    $('.alert-popup').each(function() {
        var popid = this.id;
        if (popupcookie.indexOf(popid) == -1) {
            var popup = $(this);
            popup.show(1500);
            popup.find('a').click(function(ev) {
                $.cookie('alertpopups_lib', popupcookie + popid, consentCookieOptions);
                if ($(this).attr('class') != 'alert-popup-message') {
                    ev.preventDefault();
                }
                popup.hide(500);
            });
        }
    });

 })(jQuery);

// Truncate long text
(function ($) {

if ($.fn.expander) {
    $('div.truncate').expander({
        slicePoint: 800, // default is 100
        expandPrefix: ' ... ', // default is '... '
        expandText: 'Read More', // default is 'read more'
        userCollapseText: '' // default is 'read less'
    });
}

})(jQuery);

// to avoid loading effects
(function ($) {
$.fn.highlight = function () {
    $(this).each(function () {
        var el = $(this);
        el.before("<div/>");
        el.prev()
        .width(el.width())
        .height(el.height())
        .css({
            "position": "absolute",
            "background-color": "#ffff99",
            "opacity": ".9"
        })
    .fadeOut(2000);
    });
};
})(jQuery);

// Search
(function ($) {
/* *** OLD SEARCH FUNCTIONALITY *** */
// Hide the search options.
$('.search-options .search-type').addClass("hidden");

// The search options are not selected.
var searchtypeSelected = false;

    $(".search-options .search-type").hover(
        function () { searchtypeSelected = true; },
        function () {
            searchtypeSelected = false;
        });

$(".search-options .search-field").click(function () {
    $('.search-options .search-type').removeClass("hidden");
    $(".search-options").addClass("search-type-open");
});

$(".search-options .search-field").blur(function () {
    if (!searchtypeSelected) {  // if you click on anything other than the search-type
        $(".search-options .search-type").addClass("hidden");  // hide the results
        $(".search-options").removeClass("search-type-open");
    }
});

$(".search-options .search-type-close").click(function () {
    $(".search-options .search-type").addClass("hidden");  // hide the results
    $(".search-options").removeClass("search-type-open");
});

/* *** NEW SEARCH FUNCTIONALITY; EXPERIMENTAL *** */
/*
var searchCookieName = 'WL_SEARCH_TYPE';
var searchCookieVal = $.cookie(searchCookieName);
var searchWrap       = $('.search-options');
var searchField      = searchWrap.find('.search-field');
var searchType       = searchWrap.find('.search-type');
var searchArrow      = searchWrap.find('.search-type-arrow');
var searchClose      = function() {
	searchWrap.removeClass('search-type-open');
	searchType.addClass('hiding');
};
var searchOpen = function() {
	searchWrap.addClass('search-type-open');
	searchType.removeClass('hiding');
};

// Remember the user's search type selection from the cookie, if set
if ('undefined' !== typeof searchCookieVal && searchCookieVal) {
	searchType.find('input[value=' + searchCookieVal + ']').prop('checked', true);
}

// Set the placeholder correctly
searchField.attr('placeholder', searchType.find('input:checked').attr('data-placeholder'));

// Polyfill the placeholder on the search field for older browsers
Placeholders.disable();
Placeholders.enable(searchField.get(0));

// Hide the search options by default
searchClose();

// Show the search type box on focus or the input
searchField.on('focus', searchOpen);

// Show the search type box on click of the arrow
searchArrow.on('click', function() {
	searchField.focus();
});

// Close the search type box when close button or outside box is clicked
searchWrap.find('.search-type-close').add('html,body').on('click', searchClose);

// Don't close the search type box if click landed in the box or input element
searchType.add(searchField).add(searchArrow).on('click', function(e) {
	e.stopPropagation();
});

// Ensure the search type box is showing when focus is on the radio buttons
searchType.find('input').on('focus', searchOpen);

// Close the search type box when anything else is focused
$('a, input, select, button').on('focus', function() {
	if (!$.contains(searchWrap.get(0), this)) {
		searchClose();
	}
});

// Change placeholder text when search type is changed
searchType.find('input').on('change', function() {
	var placeholder = $(this).attr('data-placeholder');

	if (placeholder) {
		searchField.attr('placeholder', placeholder);
	}

	// Save cookie for the user's selection
	$.cookie(searchCookieName, $(this).val(), {
		expires: 30, // Expires in 30 days
		domain : 'wellcomelibrary.org',
	});
});

// Close the search type box after any type input is selected (even if currently selected)
searchType.find('input').on('click', function(e) {
	e.stopPropagation();
	searchClose();
});

searchType.find('input, a').on('focus', function() {
	searchOpen();
});

// Nasty hack to make IE work properly
searchType.find('label').on('click', function() {
    $(this).find('input').click();

    return false;
});
*/
})(jQuery);

/* Archive Tree Tree */
(function ($) {
    $('.archiveTree.tree ul').each(function(){
    var $parent = $('li', this).last();
    var $height = $parent.height();
    var $newHeight = $height - 20;
    $parent.append('<span class="last-mask" style="height: '+$newHeight+'px"></span>');

/* Archive Tree Collapse */
    $('.archiveTree.collapse.no-js').removeClass('no-js');
    $('.archiveTree.collapse ul').each(function(i) {
        ////
        if($(this).length) {
            $(this).before('<div class="archiveTreeCollapseItem">&#9650;</div>');
        }
        ////
    });
    $('.archiveTree.collapse').on('click', '.archiveTreeCollapseItem', function() {
        //////
        var $nextItem = $(this).next('ul');
        var $nextItemState = $(this).next('ul').data('archive-tree-state');
        //////
        if($nextItemState === 'show') {
            $(this).next('ul').slideDown();
            $nextItem.data('archive-tree-state', 'hide');
            $(this).html('&#9650;');
        }
        else {
            $(this).next('ul').slideUp();
            $nextItem.data('archive-tree-state', 'show');
            $(this).html('&#9660;');
        }
        ///////
    });
});
})(jQuery);


/* Alerts */
(function ($) {
    hasKey = function (obj, key) {
        return obj != null && hasOwnProperty.call(obj, key);
    };
    var $alerts = '';
    var showAlerts = document.querySelector('[data-show-alerts]') ? true : false;

    if (showAlerts) {
        var getAlerts = fetchAlerts();
        getAlerts.done(function (data) {
            if (hasKey(data, 'alerts')) {
                var alerts = data.alerts;
                $.each(alerts, function (key, value) {
                    var alertTemplate = null;
                    if (hasKey(value, 'targets')) {
                        var targets = value.targets;
                        // target options main, blog, catalogue
                        if (isLibrary && targets.indexOf('main') >= 0) {
                            alertTemplate = generateAlert(value);
                        }
                        if (isBlog && targets.indexOf('blog') >= 0) {
                            alertTemplate = generateAlert(value);
                        }
                        if (isCatalogue && targets.indexOf('catalogue') >= 0) {
                            alertTemplate = generateAlert(value);
                        }
                    } else {
                        // no targets - it can go in
                        alertTemplate = generateAlert(value);
                    }
                    if (alertTemplate != null) {
                        $alerts = $alerts + alertTemplate;
                    }
                });
                $('.page-wrap').prepend($alerts);
            }
        });
    }

    function generateAlert(value) {
        var alertTemplate = '<div class="alert {{classes}}"><div class="alert__row"><div class="alert__content">{{title}}{{content}}</div></div></div>';
        var title = (hasKey(value, 'title')) ? value.title : '';
        var content = (hasKey(value, 'content')) ? value.content : '';
        var classes = '';
        classes += (hasKey(value, 'size')) ? ' alert-' + value.size : '';
        classes += (hasKey(value, 'color')) ? ' ' + value.color : '';
        classes += (hasKey(value, 'icon')) ? ' has-icon icon-' + value.icon : '';
        alertTemplate = alertTemplate.replace(/{{title}}/, title);
        alertTemplate = alertTemplate.replace(/{{content}}/, content);
        alertTemplate = alertTemplate.replace(/{{classes}}/, classes);
        return alertTemplate;
    }

    function fetchAlerts() {
        //var url = '//localhost:8080/data/alerts.json';
        var url = '//wellcomelibrary.org/handlers/alerts.ashx';
        return $.ajax({
            type: 'GET',
            url: url,
            cache: false,
            dataType: 'json',
            success: function (data) {
                console.info('Successfully fetched alerts');
            },
            error: function (data) {
                console.info('Unable to fetch alerts');
            },
        });
    }

})(jQuery);