(function(i,s,o,g,r,a,m){i['GoogleAnalyticsObject']=r;i[r]=i[r]||function(){
        (i[r].q=i[r].q||[]).push(arguments)},i[r].l=1*new Date();a=s.createElement(o),
    m=s.getElementsByTagName(o)[0];a.async=1;a.src=g;m.parentNode.insertBefore(a,m)
})(window,document,'script','//www.google-analytics.com/analytics.js','ga');
ga('create', 'UA-5739409-1', 'auto');
function trackEvent(category, action, label) {
    ga('send', 'event', category, action, label);
}
function trackVariable(name, value) {
    ga('set', name, value);
}

$(function() {

    var loadObj;
    var settings;

    var playerInteractions = 'Player Interactions';
    var files = 'Files';

    $(document).bind('uv.onAcceptTerms', function (event, obj) {
        trackEvent(playerInteractions, 'Ts & Cs', 'Accepted, ' + trackingLabel);
    });

    $(document).bind('uv.onAuthorizationOccurred', function (event, obj) {
        //console.log('uv.onAuthorizationOccurred');
    });

    $(document).bind('uv.onBookmark', function (event, obj) {
        //console.log('uv.onBookmark', obj);
    });

    $(document).bind('uv.onCanvasIndexChangeFailed', function (event, obj) {
        //console.log('uv.onCanvasIndexChangeFailed');
    });

    $(document).bind('uv.onCanvasIndexChanged', function (event, obj) {
        trackEvent('Pages', 'Viewed', 'Index: ' + obj + ', ' + trackingLabel);
    });

    $(document).bind('uv.onClickthroughOccurred', function (event, obj) {
        //console.log('uv.onClickthroughOccurred');
    });

    $(document).bind('uv.onCloseActiveDialogue', function (event, obj) {
        //console.log('uv.onCloseActiveDialogue');
    });

    $(document).bind('uv.onCloseLeftPanel', function (event, obj) {
        trackEvent(playerInteractions, 'Left Panel', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onCloseRightPanel', function (event, obj) {
        trackEvent(playerInteractions, 'Right Panel', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onCreated', function (event, obj) {
        trackEvent('Items', 'Viewed', trackingLabel);

        if (!loadObj.bootstrapper.params.isHomeDomain) {
            trackVariable('Embedded', loadObj.bootstrapper.params.domain);
        }
    });

    $(document).bind('uv.onDownArrow', function (event, obj) {
        //console.log('uv.onDownArrow');
    });

    $(document).bind('uv.onDownload', function (event, obj) {
        switch(obj.type){
            case "entireDocumentAsPdf" :
                trackEvent(files, 'Downloaded - Entire Document As PDF', trackingLabel);
                break;
            case "currentView" :
                trackEvent(files, 'Downloaded - Current View', trackingLabel);
                break;
            case "wholeImageHighRes" :
                trackEvent(files, 'Downloaded - Whole Image High Res', trackingLabel);
                break;
            case "wholeImageLowRes" :
                trackEvent(files, 'Downloaded - Whole Image Low Res', trackingLabel);
                break;
            case "entireDocumentAsPdf" :
                trackEvent(files, 'Downloaded - Entire Document As PDF', trackingLabel);
                break;
            case "entireDocumentAsText" :
                trackEvent(files, 'Downloaded - Entire Document As Text', trackingLabel);
                break;
        }
    });

    $(document).bind('uv.onDrop', function (event, manifestUri) {
        //console.log('uv.drop: ' + manifestUri);
    });

    $(document).bind('uv.onEnd', function (event, obj) {
        //console.log('uv.onEnd');
    });

    $(document).bind('uv.onEscape', function (event, obj) {
        //console.log('uv.onEscape');
    });

    $(document).bind('uv.onExternalLinkClicked', function (event, url) {
        if (url.indexOf('terms-and-conditions') != -1){
            trackEvent(playerInteractions, 'Ts & Cs', 'Loaded, ' + trackingLabel);
        }
    });

    $(document).bind('uv.onFeedback', function (event, obj) {
        //console.log('uv.onFeedback', obj);
    });

    $(document).bind('uv.onHideClickthroughDialogue', function (event, obj) {
        //console.log('uv.onHideClickthroughDialogue');
    });

    $(document).bind('uv.onHideDownloadDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Download', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onHideEmbedDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Embed', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onHideExternalContentDialogue', function (event, obj) {
        //console.log('uv.onHideExternalContentDialogue');
    });

    $(document).bind('uv.onHideGenericDialogue', function (event, obj) {
        //console.log('uv.onHideGenericDialogue');
    });

    $(document).bind('uv.onHideInformation', function (event, obj) {
        //console.log('uv.onHideInformation');
    });

    $(document).bind('uv.onHideLoginDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Log in', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onHideOverlay', function (event, obj) {
        //console.log('uv.onHideOverlay');
    });

    $(document).bind('uv.onHideRestrictedDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Restricted file', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onHideSettingsDialogue', function (event, obj) {
        // uv uses onHideOverlay
        trackEvent(playerInteractions, 'Settings', 'Closed, ' + trackingLabel);
    });

    $(document).bind('uv.onHome', function (event, obj) {
        //console.log('uv.onHome');
    });

    $(document).bind('uv.onLeftArrow', function (event, obj) {
        //console.log('uv.onLeftArrow');
    });

    $(document).bind('uv.onLeftPanelCollapseFullFinish', function (event, obj) {
        trackEvent(playerInteractions, 'Left Panel', 'Collapsed, ' + trackingLabel);
    });

    $(document).bind('uv.onLeftPanelCollapseFullStart', function (event, obj) {
        //console.log('uv.onLeftPanelCollapseFullStart');
    });

    $(document).bind('uv.onLeftPanelExpandFullFinish', function (event, obj) {
        trackEvent(playerInteractions, 'Left Panel', 'Expanded, ' + trackingLabel);
    });

    $(document).bind('uv.onLeftPanelExpandFullStart', function (event, obj) {
        //console.log('uv.onLeftPanelExpandFullStart');
    });

    $(document).bind('uv.onLoad', function (event, obj) {
        loadObj = obj;
        settings = jQuery.extend(true, {}, obj.settings);
        trackEvent("Player", "Load", trackingLabel);
    });

    $(document).bind('uv.onNotFound', function (event, obj) {
        //console.log('uv.onNotFound');
    });

    $(document).bind('uv.onMinus', function (event, obj) {
        //console.log('uv.onMinus');
    });

    $(document).bind('uv.onOpenLeftPanel', function (event, obj) {
        trackEvent(playerInteractions, 'Left Panel', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onOpenExternalResource', function (event, obj) {
        //console.log('uv.onOpenExternalResource');
    });

    $(document).bind('uv.onOpenRightPanel', function (event, obj) {
        trackEvent(playerInteractions, 'Right Panel', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onPageDown', function (event, obj) {
        //console.log('uv.onPageDown');
    });

    $(document).bind('uv.onPageUp', function (event, obj) {
        //console.log('uv.onPageUp');
    });

    $(document).bind('uv.onPlus', function (event, obj) {
        //console.log('uv.onPlus');
    });

    $(document).bind('uv.onRedirect', function (event, obj) {
        //console.log('uv.onRedirect');
    });

    $(document).bind('uv.onRefresh', function (event, obj) {
        //console.log('uv.onRefresh');
    });

    $(document).bind('uv.onResourceDegraded', function (event, obj) {
        //console.log('uv.onResourceDegraded');
    });

    $(document).bind('uv.onReturn', function (event, obj) {
        //console.log('uv.onReturn');
    });

    $(document).bind('uv.onRightArrow', function (event, obj) {
        //console.log('uv.onRightArrow');
    });

    $(document).bind('uv.onRightPanelCollapseFullFinish', function (event, obj) {
        //console.log('uv.onRightPanelCollapseFullFinish');
    });

    $(document).bind('uv.onRightPanelCollapseFullStart', function (event, obj) {
        //console.log('uv.onRightPanelCollapseFullStart');
    });

    $(document).bind('uv.onRightPanelExpandFullFinish', function (event, obj) {
        //console.log('uv.onRightPanelExpandFullFinish');
    });

    $(document).bind('uv.onRightPanelExpandFullStart', function (event, obj) {
        //console.log('uv.onRightPanelExpandFullStart');
    });

    $(document).bind('uv.onSequenceIndexChanged', function (event, sequenceIndex) {
        //console.log('uv.onSequenceIndexChanged: ' + sequenceIndex);
    });

    $(document).bind('uv.onSettingsChanged', function (event, obj) {

        if (settings.pagingEnabled !== obj.pagingEnabled) {
            if (obj.pagingEnabled) {
                trackEvent(playerInteractions, 'Two Page View Enabled', 'True, ' + trackingLabel);
            } else {
                trackEvent(playerInteractions, 'Two Page View Enabled', 'False, ' + trackingLabel);
            }
        }

        if (settings.navigatorEnabled !== obj.navigatorEnabled) {
            if (obj.navigatorEnabled) {
                trackEvent(playerInteractions, 'Navigator Enabled', 'True, ' + trackingLabel);
            } else {
                trackEvent(playerInteractions, 'Navigator Enabled', 'False, ' + trackingLabel);
            }
        }

        if (settings.preserveViewport !== obj.preserveViewport) {
            if (obj.preserveViewport) {
                trackEvent(playerInteractions, 'Preserve Viewport Enabled', 'True, ' + trackingLabel);
            } else {
                trackEvent(playerInteractions, 'Preserve Viewport Enabled', 'False, ' + trackingLabel);
            }
        }

        settings = jQuery.extend(true, {}, obj);
    });

    $(document).bind('uv.onShowClickThroughDialogue', function (event, obj) {
        trackEvent(playerInteractions,  'Ts & Cs', 'Viewed, ' + trackingLabel);
    });

    $(document).bind('uv.onShowDownloadDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Download', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onShowEmbedDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Embed', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onShowExternalContentDialogue', function (event, obj) {
        //console.log('uv.onShowExternalContentDialogue');
    });

    $(document).bind('uv.onShowGenericDialogue', function (event, obj) {
        //console.log('uv.onShowGenericDialogue');
    });

    $(document).bind('uv.onShowHelpDialogue', function (event, obj) {
        //console.log('uv.onShowHelpDialogue');
    });

    $(document).bind('uv.onShowInformation', function (event, obj) {
        //console.log('uv.onShowInformation');
    });

    $(document).bind('uv.onShowLoginDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Log in', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onShowOverlay', function (event, obj) {
        //console.log('uv.onShowOverlay');
    });

    $(document).bind('uv.onShowRestrictedDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Restricted file', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onShowSettingsDialogue', function (event, obj) {
        trackEvent(playerInteractions, 'Settings', 'Opened, ' + trackingLabel);
    });

    $(document).bind('uv.onThumbSelected', function (event, obj) {
        //console.log('uv.onThumbSelected');
    });

    $(document).bind('uv.onToggleFullScreen', function (event, obj) {
        if (obj.isFullScreen) {
            trackEvent(playerInteractions, 'Full Screen', 'Enter, ' + trackingLabel);
        } else {
            trackEvent(playerInteractions, 'Full Screen', 'Exit, ' + trackingLabel);
        }
    });

    $(document).bind('uv.onUpArrow', function (event, obj) {
        //console.log('uv.onUpArrow');
    });

    $(document).bind('uv.onUpdateSettings', function (event, obj) {
        //console.log('uv.onUpdateSettings');
    });

    $(document).bind('uv.onViewFullTerms', function (event, obj) {
        trackEvent(playerInteractions, 'Ts & Cs', 'Viewed, ' + trackingLabel);
    });

    $(document).bind('uv.onWindowUnload', function (event, obj) {
        //console.log('uv.onWindowUnload');
    });

    $(document).bind('seadragonExtension.onClearSearch', function (event, obj) {
        //console.log('seadragonExtension.onClearSearch');
    });

    $(document).bind('seadragonExtension.onOpenThumbsView', function (event, obj) {
        trackEvent(playerInteractions, 'Thumbs', 'Opened, ' + trackingLabel);
        //trackEvent(playerInteractions, 'Tree', 'Closed, ' + trackingLabel);
    });

    $(document).bind('seadragonExtension.onCurrentViewUri', function (event, obj) {
        //console.log('seadragonExtension.onCurrentViewUri');
    });

    $(document).bind('seadragonExtension.onFirst', function (event, obj) {
        //console.log('seadragonExtension.onFirst');
    });

    $(document).bind('seadragonExtension.onGalleryDecreaseSize', function (event, obj) {
        trackEvent(playerInteractions, 'Thumbnails size', 'Smaller, ' + trackingLabel);
    });

    $(document).bind('seadragonExtension.onGalleryIncreaseSize', function (event, obj) {
        trackEvent(playerInteractions, 'Thumbnails size', 'Larger, ' + trackingLabel);
    });

    $(document).bind('seadragonExtension.onGalleryThumbSelected', function (event, obj) {
        //console.log('seadragonExtension.onGalleryThumbSelected');
    });

    $(document).bind('seadragonExtension.onImageSearch', function (event, obj) {
        //console.log('seadragonExtension.onImageSearch');
    });

    $(document).bind('seadragonExtension.onLast', function (event, obj) {
        //console.log('seadragonExtension.onLast');
    });

    $(document).bind('seadragonExtension.onModeChanged', function (event, obj) {
        //console.log('seadragonExtension.onModeChanged', obj);
    });

    $(document).bind('seadragonExtension.onMultiSelectionMade', function (event, ids) {
        //console.log('seadragonExtension.onMultiSelectionMade', ids);
    });

    $(document).bind('seadragonExtension.onNext', function (event, obj) {
        //console.log('seadragonExtension.onNext');
    });

    $(document).bind('seadragonExtension.onNextSearchResult', function (event, obj) {
        //console.log('seadragonExtension.onNextSearchResult');
    });

    $(document).bind('seadragonExtension.onOpenTreeView', function (event, obj) {
        trackEvent(playerInteractions, 'Tree', 'Opened, ' + trackingLabel);
        //trackEvent(playerInteractions, 'Thumbs', 'Closed, ' + trackingLabel);
    });

    $(document).bind('seadragonExtension.onPageSearch', function (event, obj) {
        //console.log('seadragonExtension.onPageSearch');
    });

    $(document).bind('seadragonExtension.onPagingToggled', function (event, obj) {
        if (obj){
            trackEvent(playerInteractions, 'Two page view', 'Opened, ' + trackingLabel);
        } else {
            trackEvent(playerInteractions, 'Two page view', 'Closed, ' + trackingLabel);
        }
    });

    $(document).bind('seadragonExtension.onPrev', function (event, obj) {
        //console.log('seadragonExtension.onPrev');
    });

    $(document).bind('seadragonExtension.onPrevSearchResult', function (event, obj) {
        //console.log('seadragonExtension.onPrevSearchResult');
    });

    $(document).bind('seadragonExtension.onAnimation', function (event, obj) {
        //console.log('seadragonExtension.onAnimation');
    });

    $(document).bind('seadragonExtension.onAnimationfinish', function (event, obj) {
        //console.log('seadragonExtension.onAnimationfinish');
    });

    $(document).bind('seadragonExtension.onAnimationStart', function (event, obj) {
        //console.log('seadragonExtension.onAnimationStart');
    });

    $(document).bind('seadragonExtension.onOpen', function (event, obj) {
        //console.log('seadragonExtension.onOpen');
    });

    $(document).bind('seadragonExtension.onSearchPreviewStart', function (event, obj) {
        //console.log('seadragonExtension.onSearchPreviewStart');
    });

    $(document).bind('seadragonExtension.onSearchPreviewFinish', function (event, obj) {
        //console.log('seadragonExtension.onSearchPreviewFinish');
    });

    $(document).bind('seadragonExtension.onRotation', function (event, obj) {
        //console.log('seadragonExtension.onRotation');
    });

    $(document).bind('seadragonExtension.onSearch', function (event, obj) {
        //console.log('seadragonExtension.onSearch', obj);
    });

    $(document).bind('seadragonExtension.onSearchResults', function (event, obj) {
        //console.log('seadragonExtension.onSearchResults', obj);
    });

    $(document).bind('seadragonExtension.onSearchResultsEmpty', function (event, obj) {
        //console.log('seadragonExtension.onSearchResultsEmpty');
    });

    $(document).bind('seadragonExtension.onTreeNodeSelected', function (event, obj) {
        //console.log('seadragonExtension.onTreeNodeSelected', obj);
    });

    $(document).bind('seadragonExtension.onViewPage', function (event, obj) {
        //console.log('seadragonExtension.onViewPage', obj);
    });

    $(document).bind('seadragonExtension.onCurrentViewUri', function (event, obj) {
        //console.log('seadragonExtension.onCurrentViewUri', obj);
    });

    $(document).bind('mediaelementExtension.onMediaEnded', function (event, obj) {
        trackEvent(playerInteractions, 'Ended', trackingLabel);
    });

    $(document).bind('mediaelementExtension.onMediaPaused', function (event, obj) {
        trackEvent(playerInteractions, 'Pause', trackingLabel);
    });

    $(document).bind('mediaelementExtension.onMediaPlayed', function (event, obj) {
        trackEvent(playerInteractions, 'Play', trackingLabel);
    });

});
