// Thumbnail Gallery

// set these at point of embed
// window.flickrThumbsSet      = '72157627184364045';
// window.flickrThumbsUserid   = '26127598%40N04';
window.flickrThumbsPerPage     = 14;
window.flickrThumbsCurrentPage = 1;

function loadImage(url, callback) {
	var img    = new Image();
	img.src    = url;
	img.onload = function() {
		return callback(img);
	};
}

function ajaxProcess() {

	/* Thumbnail gallery */
	var apikey     = '471fb035e50813512e5a70243f87bfae';
	var method     = 'flickr.photosets.getPhotos';
	var set        = window.flickrThumbsSet;
	var userid     = window.flickrThumbsUserid;
	var limit      = '1000';
	var url        = 'https://api.flickr.com/services/rest/?method=' + method + '&api_key=' + apikey + '&photoset_id=' + set + '&per_page=' + limit + '&format=json&extras=description&nojsoncallback=1';
	var ajaxURL    = url + '&per_page=' + window.flickrThumbsPerPage + '&page=' + window.flickrThumbsCurrentPage;
	var $gallery   = $('.thumbnails');
	var thumbWidth = $gallery.find('figure img').width();

	// If there are no thumbnails yet, add a fake one so we know the target width
	if (null === thumbWidth) {
		// The width is twice as much without the 1ms timeout. No idea why.
		setTimeout(function() {
			thumbWidth = $gallery.find('.grid-sizer').innerWidth();
		}, 1);
	}

	$.ajax({
		url:      ajaxURL,
		dataType: "jsonp",
		jsonp:    "jsoncallback",
		success:  function(data) {
			if(data.stat!="fail") {
				var totalLoaded = 0; // counter for number of images that are loaded

				$.each(data.photoset.photo, function(i,photo) {
					var self       = this;
					var baseUrl    = 'https://farm' + this.farm + '.static.flickr.com/' + this.server + '/' + this.id + '_' + this.secret + '_';
					var thumbIndex = 0;
					var thumbUrls  = [
						baseUrl + 'm.jpg',
						baseUrl + 'n.jpg',
						baseUrl + 'z.jpg',
					];

					var addThumb = function(thumbImg) {
						++totalLoaded;

						var imagefull    = baseUrl + 'b.jpg';
						var link         = $('<a href="' + imagefull + '" class="figure" ' + 'data-lightbox="lightbox">').appendTo($gallery);
						var figure       = $('<figure>').fadeIn(1500).appendTo(link).prepend(thumbImg);
						var figcaption   = $('<figcaption class="caption"><h4 class="media-title">' + self.title + '</h4><div class="media-description">' + self.description._content.replace(/\n/g, "<br/>") +'</div><p class="media-attribution">View image on <a href="https://www.flickr.com/photos/' + userid + '/' + self.id + '/in/set-72157641564542064">Flickr</a></p></figcaption>').appendTo(figure);

						// If we're also on the last photo, let's initialise masonry
						if (totalLoaded >= data.photoset.photo.length) {
							var container = document.querySelector('.thumbnails');
							imagesLoaded(container, function() {
								new Masonry(container, {
									itemSelector: '.figure',
									columnWidth:  container.querySelector('.grid-sizer'),
									// isFitWidth:   true, // commented out to make scaling better
								});
							});
						}
					};

					var handleThumb = function(thumbImg) {
						if (thumbImg.width >= thumbWidth || thumbIndex == (thumbUrls.length - 1)) {
							addThumb(thumbImg);
						}
						else {
							++thumbIndex;
							loadImage(thumbUrls[thumbIndex], handleThumb);
						}

					};

					loadImage(thumbUrls[thumbIndex], handleThumb);
				});

				// If we've got all the images now, disable the load button
				if ((window.flickrThumbsPerPage * window.flickrThumbsCurrentPage) >= new Number(data.photoset.total)) {
					$("#flickrThumbsNext").remove();
				}
			}
		}
	});
}

function debounce(fn, delay) {
	var timer = null;
	return function () {
		var context = this, args = arguments;
		clearTimeout(timer);
		timer = setTimeout(function () {
			fn.apply(context, args);
		}, delay);
	};
}

$(document).ready(function() {
	var $flickrThumbsGallery = $('.thumbnails');
	if ($flickrThumbsGallery.length) {
		$('<nav class="nav-infinite"><a class="button" href="#" id="flickrThumbsNext">Show more images</a></nav>').prependTo($flickrThumbsGallery);
		$('<div class="grid-sizer"></div>').appendTo($flickrThumbsGallery);

		pushTrackEvent({
			category: "Gallery Interactions",
			action: "loaded",
			label: "Initial thumbnails load for gallery on " + document.URL
		});

		ajaxProcess();

		$("#flickrThumbsNext").click(function() {
			window.flickrThumbsCurrentPage++;
			ajaxProcess();
			pushTrackEvent({
					category: "Gallery Interactions",
					action: "show more images",
					label: "Load page " + (window.flickrThumbsCurrentPage - 1) + " for gallery on " + document.URL
				});
			return false;
		});
	}
});
