/*!
 * Draggabilly PACKAGED v1.0.5
 * Make that shiz draggable
 * http://draggabilly.desandro.com
 */

/*!
 * classie - class helper functions
 * from bonzo https://github.com/ded/bonzo
 * 
 * classie.has( elem, 'my-class' ) -> true/false
 * classie.add( elem, 'my-new-class' )
 * classie.remove( elem, 'my-unwanted-class' )
 * classie.toggle( elem, 'my-class' )
 */

/*jshint browser: true, strict: true, undef: true */
/*global define: false */

( function( window ) {

    'use strict';

    // class helper functions from bonzo https://github.com/ded/bonzo

    function classReg( className ) {
        return new RegExp("(^|\\s+)" + className + "(\\s+|$)");
    }

    // classList support for class management
    // altho to be fair, the api sucks because it won't accept multiple classes at once
    var hasClass, addClass, removeClass;

    if ( 'classList' in document.documentElement ) {
        hasClass = function( elem, c ) {
            return elem.classList.contains( c );
        };
        addClass = function( elem, c ) {
            elem.classList.add( c );
        };
        removeClass = function( elem, c ) {
            elem.classList.remove( c );
        };
    }
    else {
        hasClass = function( elem, c ) {
            return classReg( c ).test( elem.className );
        };
        addClass = function( elem, c ) {
            if ( !hasClass( elem, c ) ) {
                elem.className = elem.className + ' ' + c;
            }
        };
        removeClass = function( elem, c ) {
            elem.className = elem.className.replace( classReg( c ), ' ' );
        };
    }

    function toggleClass( elem, c ) {
        var fn = hasClass( elem, c ) ? removeClass : addClass;
        fn( elem, c );
    }

    var classie = {
        // full names
        hasClass:       hasClass,
        addClass:       addClass,
        removeClass:    removeClass,
        toggleClass:    toggleClass,
        // short names
        has:            hasClass,
        add:            addClass,
        remove:         removeClass,
        toggle:         toggleClass
    };

    // transport
    if ( typeof define === 'function' && define.amd ) {
        // AMD
        define( classie );
    } else {
        // browser global
        window.classie = classie;
    }

})( window );

/*!
 * eventie v1.0.3
 * event binding helper
 *   eventie.bind( elem, 'click', myFn )
 *   eventie.unbind( elem, 'click', myFn )
 */

/*jshint browser: true, undef: true, unused: true */
/*global define: false */

( function( window ) {

    'use strict';

    var docElem = document.documentElement;

    var bind = function() {};

    if ( docElem.addEventListener ) {
        bind = function( obj, type, fn ) {
            obj.addEventListener( type, fn, false );
        };
    }
    else if ( docElem.attachEvent ) {
        bind = function( obj, type, fn ) {
            obj[ type + fn ] = fn.handleEvent ?
            function() {
                var event = window.event;
                // add event.target
                event.target = event.target || event.srcElement;
                fn.handleEvent.call( fn, event );
            } :
            function() {
                var event = window.event;
                // add event.target
                event.target = event.target || event.srcElement;
                fn.call( obj, event );
            };
            obj.attachEvent( "on" + type, obj[ type + fn ] );
        };
    }

    var unbind = function() {};

    if ( docElem.removeEventListener ) {
        unbind = function( obj, type, fn ) {
            obj.removeEventListener( type, fn, false );
        };
    }
    else if ( docElem.detachEvent ) {
        unbind = function( obj, type, fn ) {
            obj.detachEvent( "on" + type, obj[ type + fn ] );
            try {
                delete obj[ type + fn ];
            }
            catch ( err ) {
                // can't delete window object properties
                obj[ type + fn ] = undefined;
            }
        };
    }

    var eventie = {
        bind: bind,
        unbind: unbind
    };

    // transport
    if ( typeof define === 'function' && define.amd ) {
        // AMD
        define( eventie );
    }
    else {
        // browser global
        window.eventie = eventie;
    }

})( this );

/*!
 * EventEmitter v4.2.3 - git.io/ee
 * Oliver Caldwell
 * MIT license
 * @preserve
 */

(function () {
    'use strict';

    /**
     * Class for managing events.
     * Can be extended to provide event functionality in other classes.
     *
     * @class EventEmitter Manages event registering and emitting.
     */
    function EventEmitter() {}

    // Shortcuts to improve speed and size

    // Easy access to the prototype
    var proto = EventEmitter.prototype;

    /**
     * Finds the index of the listener for the event in it's storage array.
     *
     * @param {Function[]} listeners Array of listeners to search through.
     * @param {Function} listener Method to look for.
     * @return {Number} Index of the specified listener, -1 if not found
     * @api private
     */
    function indexOfListener(listeners, listener) {
        var i = listeners.length;
        while (i--) {
            if (listeners[i].listener === listener) {
                return i;
            }
        }

        return -1;
    }

    /**
     * Alias a method while keeping the context correct, to allow for overwriting of target method.
     *
     * @param {String} name The name of the target method.
     * @return {Function} The aliased method
     * @api private
     */
    function alias(name) {
        return function aliasClosure() {
            return this[name].apply(this, arguments);
        };
    }

    /**
     * Returns the listener array for the specified event.
     * Will initialise the event object and listener arrays if required.
     * Will return an object if you use a regex search. The object contains keys for each matched event. So /ba[rz]/ might return an object containing bar and baz. But only if you have either defined them with defineEvent or added some listeners to them.
     * Each property in the object response is an array of listener functions.
     *
     * @param {String|RegExp} evt Name of the event to return the listeners from.
     * @return {Function[]|Object} All listener functions for the event.
     */
    proto.getListeners = function getListeners(evt) {
        var events = this._getEvents();
        var response;
        var key;

        // Return a concatenated array of all matching events if
        // the selector is a regular expression.
        if (typeof evt === 'object') {
            response = {};
            for (key in events) {
                if (events.hasOwnProperty(key) && evt.test(key)) {
                    response[key] = events[key];
                }
            }
        }
        else {
            response = events[evt] || (events[evt] = []);
        }

        return response;
    };

    /**
     * Takes a list of listener objects and flattens it into a list of listener functions.
     *
     * @param {Object[]} listeners Raw listener objects.
     * @return {Function[]} Just the listener functions.
     */
    proto.flattenListeners = function flattenListeners(listeners) {
        var flatListeners = [];
        var i;

        for (i = 0; i < listeners.length; i += 1) {
            flatListeners.push(listeners[i].listener);
        }

        return flatListeners;
    };

    /**
     * Fetches the requested listeners via getListeners but will always return the results inside an object. This is mainly for internal use but others may find it useful.
     *
     * @param {String|RegExp} evt Name of the event to return the listeners from.
     * @return {Object} All listener functions for an event in an object.
     */
    proto.getListenersAsObject = function getListenersAsObject(evt) {
        var listeners = this.getListeners(evt),
            response;

        if (listeners instanceof Array) {
            response = {};
            response[evt] = listeners;
        }

        return response || listeners;
    };

    /**
     * Adds a listener function to the specified event.
     * The listener will not be added if it is a duplicate.
     * If the listener returns true then it will be removed after it is called.
     * If you pass a regular expression as the event name then the listener will be added to all events that match it.
     *
     * @param {String|RegExp} evt Name of the event to attach the listener to.
     * @param {Function} listener Method to be called when the event is emitted. If the function returns true then it will be removed after calling.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.addListener = function addListener(evt, listener) {
        var listeners = this.getListenersAsObject(evt),
            listenerIsWrapped = typeof listener === 'object',
            key;

        for (key in listeners) {
            if (listeners.hasOwnProperty(key) && indexOfListener(listeners[key], listener) === -1) {
                listeners[key].push(listenerIsWrapped ? listener : {
                    listener: listener,
                    once: false
                });
            }
        }

        return this;
    };

    /**
     * Alias of addListener
     */
    proto.on = alias('addListener');

    /**
     * Semi-alias of addListener. It will add a listener that will be
     * automatically removed after it's first execution.
     *
     * @param {String|RegExp} evt Name of the event to attach the listener to.
     * @param {Function} listener Method to be called when the event is emitted. If the function returns true then it will be removed after calling.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.addOnceListener = function addOnceListener(evt, listener) {
        return this.addListener(evt, {
            listener: listener,
            once: true
        });
    };

    /**
     * Alias of addOnceListener.
     */
    proto.once = alias('addOnceListener');

    /**
     * Defines an event name. This is required if you want to use a regex to add a listener to multiple events at once. If you don't do this then how do you expect it to know what event to add to? Should it just add to every possible match for a regex? No. That is scary and bad.
     * You need to tell it what event names should be matched by a regex.
     *
     * @param {String} evt Name of the event to create.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.defineEvent = function defineEvent(evt) {
        this.getListeners(evt);
        return this;
    };

    /**
     * Uses defineEvent to define multiple events.
     *
     * @param {String[]} evts An array of event names to define.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.defineEvents = function defineEvents(evts) {
        for (var i = 0; i < evts.length; i += 1) {
            this.defineEvent(evts[i]);
        }
        return this;
    };

    /**
     * Removes a listener function from the specified event.
     * When passed a regular expression as the event name, it will remove the listener from all events that match it.
     *
     * @param {String|RegExp} evt Name of the event to remove the listener from.
     * @param {Function} listener Method to remove from the event.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.removeListener = function removeListener(evt, listener) {
        var listeners = this.getListenersAsObject(evt);
        var index;
        var key;

        for (key in listeners) {
            if (listeners.hasOwnProperty(key)) {
                index = indexOfListener(listeners[key], listener);

                if (index !== -1) {
                    listeners[key].splice(index, 1);
                }
            }
        }

        return this;
    };

    /**
     * Alias of removeListener
     */
    proto.off = alias('removeListener');

    /**
     * Adds listeners in bulk using the manipulateListeners method.
     * If you pass an object as the second argument you can add to multiple events at once. The object should contain key value pairs of events and listeners or listener arrays. You can also pass it an event name and an array of listeners to be added.
     * You can also pass it a regular expression to add the array of listeners to all events that match it.
     * Yeah, this function does quite a bit. That's probably a bad thing.
     *
     * @param {String|Object|RegExp} evt An event name if you will pass an array of listeners next. An object if you wish to add to multiple events at once.
     * @param {Function[]} [listeners] An optional array of listener functions to add.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.addListeners = function addListeners(evt, listeners) {
        // Pass through to manipulateListeners
        return this.manipulateListeners(false, evt, listeners);
    };

    /**
     * Removes listeners in bulk using the manipulateListeners method.
     * If you pass an object as the second argument you can remove from multiple events at once. The object should contain key value pairs of events and listeners or listener arrays.
     * You can also pass it an event name and an array of listeners to be removed.
     * You can also pass it a regular expression to remove the listeners from all events that match it.
     *
     * @param {String|Object|RegExp} evt An event name if you will pass an array of listeners next. An object if you wish to remove from multiple events at once.
     * @param {Function[]} [listeners] An optional array of listener functions to remove.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.removeListeners = function removeListeners(evt, listeners) {
        // Pass through to manipulateListeners
        return this.manipulateListeners(true, evt, listeners);
    };

    /**
     * Edits listeners in bulk. The addListeners and removeListeners methods both use this to do their job. You should really use those instead, this is a little lower level.
     * The first argument will determine if the listeners are removed (true) or added (false).
     * If you pass an object as the second argument you can add/remove from multiple events at once. The object should contain key value pairs of events and listeners or listener arrays.
     * You can also pass it an event name and an array of listeners to be added/removed.
     * You can also pass it a regular expression to manipulate the listeners of all events that match it.
     *
     * @param {Boolean} remove True if you want to remove listeners, false if you want to add.
     * @param {String|Object|RegExp} evt An event name if you will pass an array of listeners next. An object if you wish to add/remove from multiple events at once.
     * @param {Function[]} [listeners] An optional array of listener functions to add/remove.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.manipulateListeners = function manipulateListeners(remove, evt, listeners) {
        var i;
        var value;
        var single = remove ? this.removeListener : this.addListener;
        var multiple = remove ? this.removeListeners : this.addListeners;

        // If evt is an object then pass each of it's properties to this method
        if (typeof evt === 'object' && !(evt instanceof RegExp)) {
            for (i in evt) {
                if (evt.hasOwnProperty(i) && (value = evt[i])) {
                    // Pass the single listener straight through to the singular method
                    if (typeof value === 'function') {
                        single.call(this, i, value);
                    }
                    else {
                        // Otherwise pass back to the multiple function
                        multiple.call(this, i, value);
                    }
                }
            }
        }
        else {
            // So evt must be a string
            // And listeners must be an array of listeners
            // Loop over it and pass each one to the multiple method
            i = listeners.length;
            while (i--) {
                single.call(this, evt, listeners[i]);
            }
        }

        return this;
    };

    /**
     * Removes all listeners from a specified event.
     * If you do not specify an event then all listeners will be removed.
     * That means every event will be emptied.
     * You can also pass a regex to remove all events that match it.
     *
     * @param {String|RegExp} [evt] Optional name of the event to remove all listeners for. Will remove from every event if not passed.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.removeEvent = function removeEvent(evt) {
        var type = typeof evt;
        var events = this._getEvents();
        var key;

        // Remove different things depending on the state of evt
        if (type === 'string') {
            // Remove all listeners for the specified event
            delete events[evt];
        }
        else if (type === 'object') {
            // Remove all events matching the regex.
            for (key in events) {
                if (events.hasOwnProperty(key) && evt.test(key)) {
                    delete events[key];
                }
            }
        }
        else {
            // Remove all listeners in all events
            delete this._events;
        }

        return this;
    };

    /**
     * Emits an event of your choice.
     * When emitted, every listener attached to that event will be executed.
     * If you pass the optional argument array then those arguments will be passed to every listener upon execution.
     * Because it uses `apply`, your array of arguments will be passed as if you wrote them out separately.
     * So they will not arrive within the array on the other side, they will be separate.
     * You can also pass a regular expression to emit to all events that match it.
     *
     * @param {String|RegExp} evt Name of the event to emit and execute listeners for.
     * @param {Array} [args] Optional array of arguments to be passed to each listener.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.emitEvent = function emitEvent(evt, args) {
        var listeners = this.getListenersAsObject(evt);
        var listener;
        var i;
        var key;
        var response;

        for (key in listeners) {
            if (listeners.hasOwnProperty(key)) {
                i = listeners[key].length;

                while (i--) {
                    // If the listener returns true then it shall be removed from the event
                    // The function is executed either with a basic call or an apply if there is an args array
                    listener = listeners[key][i];

                    if (listener.once === true) {
                        this.removeListener(evt, listener.listener);
                    }

                    response = listener.listener.apply(this, args || []);

                    if (response === this._getOnceReturnValue()) {
                        this.removeListener(evt, listener.listener);
                    }
                }
            }
        }

        return this;
    };

    /**
     * Alias of emitEvent
     */
    proto.trigger = alias('emitEvent');

    /**
     * Subtly different from emitEvent in that it will pass its arguments on to the listeners, as opposed to taking a single array of arguments to pass on.
     * As with emitEvent, you can pass a regex in place of the event name to emit to all events that match it.
     *
     * @param {String|RegExp} evt Name of the event to emit and execute listeners for.
     * @param {...*} Optional additional arguments to be passed to each listener.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.emit = function emit(evt) {
        var args = Array.prototype.slice.call(arguments, 1);
        return this.emitEvent(evt, args);
    };

    /**
     * Sets the current value to check against when executing listeners. If a
     * listeners return value matches the one set here then it will be removed
     * after execution. This value defaults to true.
     *
     * @param {*} value The new value to check for when executing listeners.
     * @return {Object} Current instance of EventEmitter for chaining.
     */
    proto.setOnceReturnValue = function setOnceReturnValue(value) {
        this._onceReturnValue = value;
        return this;
    };

    /**
     * Fetches the current value to check against when executing listeners. If
     * the listeners return value matches this one then it should be removed
     * automatically. It will return true by default.
     *
     * @return {*|Boolean} The current value to check for or the default, true.
     * @api private
     */
    proto._getOnceReturnValue = function _getOnceReturnValue() {
        if (this.hasOwnProperty('_onceReturnValue')) {
            return this._onceReturnValue;
        }
        else {
            return true;
        }
    };

    /**
     * Fetches the events object and creates one if required.
     *
     * @return {Object} The events storage object.
     * @api private
     */
    proto._getEvents = function _getEvents() {
        return this._events || (this._events = {});
    };

    // Expose the class either via AMD, CommonJS or the global object
    if (typeof define === 'function' && define.amd) {
        define(function () {
            return EventEmitter;
        });
    }
    else if (typeof module === 'object' && module.exports){
        module.exports = EventEmitter;
    }
    else {
        this.EventEmitter = EventEmitter;
    }
}.call(this));

/*!
 * getStyleProperty by kangax
 * http://perfectionkills.com/feature-testing-css-properties/
 */

/*jshint browser: true, strict: true, undef: true */
/*globals define: false */

( function( window ) {

    'use strict';

    var prefixes = 'Webkit Moz ms Ms O'.split(' '),
        docElemStyle = document.documentElement.style;

    function getStyleProperty( propName ) {
        if ( !propName ) {
            return;
        }

        // test standard property first
        if ( typeof docElemStyle[ propName ] === 'string' ) {
            return propName;
        }

        // capitalize
        propName = propName.charAt(0).toUpperCase() + propName.slice(1);

        // test vendor specific properties
        var prefixed;
        for ( var i=0, len = prefixes.length; i < len; i++ ) {
            prefixed = prefixes[i] + propName;
            if ( typeof docElemStyle[ prefixed ] === 'string' ) {
                return prefixed;
            }
        }
    }

    // transport
    if ( typeof define === 'function' && define.amd ) {
        // AMD
        define( function() {
            return getStyleProperty;
        });
    }
    else {
        // browser global
        window.getStyleProperty = getStyleProperty;
    }

})( window );

/**
 * getSize v1.1.4
 * measure size of elements
 */

/*jshint browser: true, strict: true, undef: true, unused: true */
/*global define: false */

( function( window, undefined ) {

    'use strict';

    // -------------------------- helpers -------------------------- //

    var defView = document.defaultView;

    var getStyle = defView && defView.getComputedStyle ?
        function( elem ) {
            return defView.getComputedStyle( elem, null );
        } :
        function( elem ) {
            return elem.currentStyle;
        };

    // get a number from a string, not a percentage
    function getStyleSize( value ) {
        var num = parseFloat( value );
        // not a percent like '100%', and a number
        var isValid = value.indexOf('%') === -1 && !isNaN( num );
        return isValid && num;
    }

    // -------------------------- measurements -------------------------- //

    var measurements = [
        'paddingLeft',
        'paddingRight',
        'paddingTop',
        'paddingBottom',
        'marginLeft',
        'marginRight',
        'marginTop',
        'marginBottom',
        'borderLeftWidth',
        'borderRightWidth',
        'borderTopWidth',
        'borderBottomWidth'
    ];

    function getZeroSize() {
        var size = {
            width: 0,
            height: 0,
            innerWidth: 0,
            innerHeight: 0,
            outerWidth: 0,
            outerHeight: 0
        };
        for ( var i=0, len = measurements.length; i < len; i++ ) {
            var measurement = measurements[i];
            size[ measurement ] = 0;
        }
        return size;
    }


    function defineGetSize( getStyleProperty ) {

        // -------------------------- box sizing -------------------------- //

        var boxSizingProp = getStyleProperty('boxSizing');
        var isBoxSizeOuter;

        /**
         * WebKit measures the outer-width on style.width on border-box elems
         * IE & Firefox measures the inner-width
         */
        ( function() {
            if ( !boxSizingProp ) {
                return;
            }

            var div = document.createElement('div');
            div.style.width = '200px';
            div.style.padding = '1px 2px 3px 4px';
            div.style.borderStyle = 'solid';
            div.style.borderWidth = '1px 2px 3px 4px';
            div.style[ boxSizingProp ] = 'border-box';

            var body = document.body || document.documentElement;
            body.appendChild( div );
            var style = getStyle( div );

            isBoxSizeOuter = getStyleSize( style.width ) === 200;
            body.removeChild( div );
        })();


        // -------------------------- getSize -------------------------- //

        function getSize( elem ) {
            // use querySeletor if elem is string
            if ( typeof elem === 'string' ) {
                elem = document.querySelector( elem );
            }

            // do not proceed on non-objects
            if ( !elem || typeof elem !== 'object' || !elem.nodeType ) {
                return;
            }

            var style = getStyle( elem );

            // if hidden, everything is 0
            if ( style.display === 'none' ) {
                return getZeroSize();
            }

            var size = {};
            size.width  = elem.offsetWidth;
            size.height = elem.offsetHeight;

            var isBorderBox = size.isBorderBox = !!( boxSizingProp &&
            style[ boxSizingProp ] && style[ boxSizingProp ] === 'border-box' );

            // get all measurements
            for ( var i=0, len = measurements.length; i < len; i++ ) {
                var measurement = measurements[i],
                    value       = style[ measurement ],
                    num         = parseFloat( value );
                // any 'auto', 'medium' value will be 0
                size[ measurement ] = !isNaN( num ) ? num : 0;
            }

            var paddingWidth    = size.paddingLeft + size.paddingRight,
                paddingHeight   = size.paddingTop + size.paddingBottom,
                marginWidth     = size.marginLeft + size.marginRight,
                marginHeight    = size.marginTop + size.marginBottom,
                borderWidth     = size.borderLeftWidth + size.borderRightWidth,
                borderHeight    = size.borderTopWidth + size.borderBottomWidth;

            var isBorderBoxSizeOuter = isBorderBox && isBoxSizeOuter;

            // overwrite width and height if we can get it from style
            var styleWidth = getStyleSize( style.width );
            if ( styleWidth !== false ) {
                size.width = styleWidth +
                // add padding and border unless it's already including it
                ( isBorderBoxSizeOuter ? 0 : paddingWidth + borderWidth );
            }

            var styleHeight = getStyleSize( style.height );
            if ( styleHeight !== false ) {
                size.height = styleHeight +
                // add padding and border unless it's already including it
                ( isBorderBoxSizeOuter ? 0 : paddingHeight + borderHeight );
            }

            size.innerWidth     = size.width - ( paddingWidth + borderWidth );
            size.innerHeight    = size.height - ( paddingHeight + borderHeight );

            size.outerWidth     = size.width + marginWidth;
            size.outerHeight    = size.height + marginHeight;

            return size;
        }

        return getSize;

    }

    // transport
    if ( typeof define === 'function' && define.amd ) {
        // AMD
        define( [ 'get-style-property/get-style-property' ], defineGetSize );
    }
    else {
        // browser global
        window.getSize = defineGetSize( window.getStyleProperty );
    }

})( window );

/*!
 * Draggabilly v1.0.5
 * Make that shiz draggable
 * http://draggabilly.desandro.com
 */

( function( window ) {

    'use strict';

    // vars
    var document = window.document;

    // -------------------------- helpers -------------------------- //

    // extend objects
    function extend( a, b ) {
        for ( var prop in b ) {
            a[ prop ] = b[ prop ];
        }
        return a;
    }

    function noop() {}

    // ----- get style ----- //

    var defView = document.defaultView;

    var getStyle = defView && defView.getComputedStyle ?
        function( elem ) {
            return defView.getComputedStyle( elem, null );
        } :
        function( elem ) {
            return elem.currentStyle;
        };


    // http://stackoverflow.com/a/384380/182183
    var isElement = ( typeof HTMLElement === 'object' ) ?
        function isElementDOM2( obj ) {
            return obj instanceof HTMLElement;
        } :
        function isElementQuirky( obj ) {
            return obj && typeof obj === 'object' && obj.nodeType === 1 && typeof obj.nodeName === 'string';
        };

    // -------------------------- definition -------------------------- //

    function draggabillyDefinition( classie, EventEmitter, eventie, getStyleProperty, getSize ) {

        // --------------------------  -------------------------- //

        function Draggabilly( element, options ) {
            this.element = element;

            this.options = extend( {}, this.options );
            extend( this.options, options );

            this._create();
        }

        // inherit EventEmitter methods
        extend( Draggabilly.prototype, EventEmitter.prototype );

        Draggabilly.prototype.options = {};

        Draggabilly.prototype._create = function() {

            // properties
            this.position = {};
            this._getPosition();

            this.startPoint = { x: 0, y: 0 };
            this.dragPoint = { x: 0, y: 0 };

            // So we can stop handles from overlapping.
            this.minX = 0;
            this.maxX = 0;

            this.startPosition = extend( {}, this.position );

            // set relative positioning
            var style = getStyle( this.element );
            if ( style.position !== 'relative' && style.position !== 'absolute' ) {
                this.element.style.position = 'relative';
            }

            this.isEnabled = true;
            this.setHandles();
            this.getSteps();
        };

        /**
         * set this.handles and bind start events to 'em
         */
        Draggabilly.prototype.setHandles = function() {
            var handle = this.element;
            // bind pointer start event
            // listen for both, for devices like Chrome Pixel which has touch and mouse events
            eventie.bind( handle, 'mousedown', this );
            eventie.bind( handle, 'touchstart', this );
        };

        Draggabilly.prototype.setMaxX = function(x) {
            this.maxX = x;
        };

        Draggabilly.prototype.setMinX = function(x) {
            this.minX = x;
        };

        Draggabilly.prototype.getSteps = function() {
            // Deal with intervals
            if( this.options.range ) {
                var range   = this.options.range[1] - this.options.range[0],
                    steps   = Math.ceil(range / this.options.interval) + 1;
                    
                this.stepRatios = [];
                for(var i = 0; i <= steps - 1; i++) {
                    this.stepRatios[i] = (i / (steps - 1)) * 100;
                }
            }
        }

        Draggabilly.prototype.getStepsPx = function() {
            // Deal with intervals
            if( this.options.range ) {
                var range   = this.options.range[1] - this.options.range[0],
                    steps   = Math.ceil(range / this.options.interval) + 1,
                    ppi     = this.containerSize.width / steps;
                    
                this.stepRatiosPx = [];
                for(var i = 0; i <= steps; i++) {
                    this.stepRatiosPx[i] = i * ppi;
                }
            }
        }

        Draggabilly.prototype.pxToPc = function(px) {
            return (px / this.containerSize.width) * 100;
        }

        // TODO replace this with a IE8 test
        var isIE8 = 'attachEvent' in document.documentElement;

        // get left/top position from style
        Draggabilly.prototype._getPosition = function() {
            // properties
            var style = getStyle( this.element );

            // var x = parseInt( style.left, 10 );
            var x = $( this.element ).position().left;
            var y = parseInt( style.top, 10 );

            // clean up 'auto' or other non-integer values
            this.position.x = isNaN( x ) ? 0 : x;
            this.position.y = isNaN( y ) ? 0 : y;
        };

        // -------------------------- events -------------------------- //

        // trigger handler methods for events
        Draggabilly.prototype.handleEvent = function( event ) {
            var method = 'on' + event.type;
            if ( this[ method ] ) {
                this[ method ]( event );
            }
        };

        // returns the touch that we're keeping track of
        Draggabilly.prototype.getTouch = function( touches ) {
            for ( var i=0, len = touches.length; i < len; i++ ) {
                var touch = touches[i];
                if ( touch.identifier === this.pointerIdentifier ) {
                    return touch;
                }
            }
        };

        // ----- start event ----- //

        Draggabilly.prototype.onmousedown = function( event ) {
            this.dragStart( event, event );
        };

        Draggabilly.prototype.ontouchstart = function( event ) {
            // disregard additional touches
            if ( this.isDragging ) {
                return;
            }

            this.dragStart( event, event.changedTouches[0] );
        };

        function setPointerPoint( point, pointer ) {
            point.x = pointer.pageX !== undefined ? pointer.pageX : pointer.clientX;
            point.y = pointer.pageY !== undefined ? pointer.pageY : pointer.clientY;
        }

        Draggabilly.prototype.getClosestStep = function(value) {
            var k   = 0,
                min = 100;

            for(var i = 0; i <= this.stepRatios.length - 1; i++) {
                if(Math.abs(this.stepRatios[i] - value) < min) {
                    min = Math.abs(this.stepRatios[i] - value);
                    k = i;
                }
            }
            return this.stepRatios[k];
        },

        Draggabilly.prototype.getClosestStepPx = function(value) {
            var k   = 0,
                min = this.options.range[1] - this.options.range[0];

            for(var i = 0; i <= this.stepRatiosPx.length - 1; i++) {
                if(Math.abs(this.stepRatiosPx[i] - value) < min) {
                    min = Math.abs(this.stepRatiosPx[i] - value);
                    k = i;
                }
            }
            return this.stepRatiosPx[k];
        },

        /**
         * drag start
         * @param {Event} event
         * @param {Event or Touch} pointer
         */
        Draggabilly.prototype.dragStart = function( event, pointer ) {
            if ( !this.isEnabled ) {
                return;
            }

            if ( event.preventDefault ) {
                event.preventDefault();
            } else {
                event.returnValue = false;
            }

            var isTouch = event.type === 'touchstart';

            // save pointer identifier to match up touch events
            this.pointerIdentifier = pointer.identifier;

            this._getPosition();

            this.measureContainment();

            // point where drag began
            setPointerPoint( this.startPoint, pointer );
            // position _when_ drag began
            this.startPosition.x = this.position.x;
            this.startPosition.y = this.position.y;

            var pc = this.pxToPc(this.startPosition.x);

            this.position.x = this.getClosestStep( pc );

            // reset left/top style
            this.setLeftPc();

            this.dragPoint.x = 0;
            this.dragPoint.y = 0;

            this.getStepsPx();

            // bind move and end events
            this._bindEvents({
                events: isTouch ? [ 'touchmove', 'touchend', 'touchcancel' ] : [ 'mousemove', 'mouseup' ],
                // IE8 needs to be bound to document
                node: event.preventDefault ? window : document
            });

            classie.add( this.element, 'is-dragging' );

            // reset isDragging flag
            this.isDragging = true;

            this.emitEvent( 'dragStart', [ this, event, pointer ] );
        };

        Draggabilly.prototype._bindEvents = function( args ) {
            for ( var i=0, len = args.events.length; i < len; i++ ) {
                var event = args.events[i];
                eventie.bind( args.node, event, this );
            }
            // save these arguments
            this._boundEvents = args;
        };

        Draggabilly.prototype._unbindEvents = function() {
            var args = this._boundEvents;
            for ( var i=0, len = args.events.length; i < len; i++ ) {
                var event = args.events[i];
                eventie.unbind( args.node, event, this );
            }
            delete this._boundEvents;
        };

        Draggabilly.prototype.measureContainment = function() {
            var containment = this.options.containment;
            if ( !containment ) {
                return;
            }

            this.size = getSize( this.element );
            var elemRect = this.element.getBoundingClientRect();

            // use element if element
            var container = isElement( containment ) ? containment :
                // fallback to querySelector if string
                typeof containment === 'string' ? document.querySelector( containment ) :
                // otherwise just `true`, use the parent
                this.element.parentNode;

            this.containerSize = getSize( container );
            var containerRect = container.getBoundingClientRect();

            this.relativeStartPosition = {
                x: elemRect.left - containerRect.left,
                y: elemRect.top  - containerRect.top
            };
        };

        // ----- move event ----- //

        Draggabilly.prototype.onmousemove = function( event ) {
            this.dragMove( event, event );
        };

        Draggabilly.prototype.ontouchmove = function( event ) {
            var touch = this.getTouch( event.changedTouches );
            if ( touch ) {
                this.dragMove( event, touch );
            }
        };

        /**
         * drag move
         * @param {Event} event
         * @param {Event or Touch} pointer
         */
        Draggabilly.prototype.dragMove = function( event, pointer ) {

            setPointerPoint( this.dragPoint, pointer );

            this.dragPoint.x -= this.startPoint.x;
            this.dragPoint.y = 0;

            if ( this.options.containment ) {
                var relX        = this.relativeStartPosition.x,
                    dragPointX  = this.dragPoint.x;

                dragPointX = Math.max( dragPointX, - relX );
                if( this.options.dragger == 'main' ) {
                    dragPointX = Math.min( dragPointX, this.containerSize.width - relX - (this.size.width - 2) );
                } else if( this.options.dragger == 'left' ) {
                    dragPointX = Math.min( dragPointX - 4, this.containerSize.width - relX );
                } else {
                    dragPointX = Math.min( dragPointX, this.containerSize.width - relX );
                }
                // console.log( { dpX: dragPointX, other: (this.containerSize.width - relX - this.size.width) } );
                
                this.dragPoint.x = dragPointX;
            }

            var pc = this.pxToPc(this.startPosition.x + this.dragPoint.x),
                px = this.getClosestStepPx( this.startPosition.x + this.dragPoint.x ),
                closestStep = this.getClosestStep( pc ),
                minStep = this.minX ? this.getClosestStep( this.minX ) : 0,
                maxStep = this.maxX ? this.getClosestStep( this.maxX ) : 0;

            this.position.x = closestStep;

            if( minStep )
                this.position.x = closestStep < minStep ? minStep : closestStep;

            if( maxStep )
                this.position.x = closestStep > maxStep ? maxStep : closestStep;

            this.dragPoint.x = px - this.startPosition.x;

            this.emitEvent( 'dragMove', [ this, event, pointer ] );

            this.positionDrag();
        };


        // ----- end event ----- //

        Draggabilly.prototype.onmouseup = function( event ) {
            this.dragEnd( event, event );
        };

        Draggabilly.prototype.ontouchend = function( event ) {
            var touch = this.getTouch( event.changedTouches );
            if ( touch ) {
                this.dragEnd( event, touch );
            }
        };

        /**
         * drag end
         * @param {Event} event
         * @param {Event or Touch} pointer
         */
        Draggabilly.prototype.dragEnd = function( event, pointer ) {
            this.isDragging = false;

            delete this.pointerIdentifier;

            this.setLeftPc();

            // remove events
            this._unbindEvents();

            classie.remove( this.element, 'is-dragging' );

            this.emitEvent( 'dragEnd', [ this, event, pointer ] );
        };

        // ----- cancel event ----- //

        // coerce to end event
        Draggabilly.prototype.ontouchcancel = function( event ) {
            var touch = this.getTouch( event.changedTouches );
            this.dragEnd( event, touch );
        };

        // left/top positioning
        Draggabilly.prototype.setLeftTop = function() {
            this.element.style.left = this.position.x + 'px';
            this.element.style.top  = this.position.y + 'px';
        };

        // left/top positioning
        Draggabilly.prototype.setLeftPc = function(pc) {
            if( pc !== undefined )
                this.position.x = pc;

            this.element.style.left = this.position.x + '%';
        };

        Draggabilly.prototype.positionDrag = Draggabilly.prototype.setLeftPc;

        return Draggabilly;

    } // end definition

    // -------------------------- transport -------------------------- //

    if ( typeof define === 'function' && define.amd ) {
        // AMD
        define( [
            'classie/classie',
            'eventEmitter/EventEmitter',
            'eventie/eventie',
            'get-style-property/get-style-property',
            'get-size/get-size'
        ],
        draggabillyDefinition );
    }
    else {
        // browser global
        window.Draggabilly = draggabillyDefinition(
            window.classie,
            window.EventEmitter,
            window.eventie,
            window.getStyleProperty,
            window.getSize
        );
    }

})( window );
