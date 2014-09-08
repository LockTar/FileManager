/*global require, alert*/
/*jslint browser:true*/

//require.config({
//    baseUrl: '/Scripts',
//    paths: {
//        knockout: 'knockout-3.2.0.debug',
//        knockoutMapper: 'knockout.mapping-latest.debug',
//        //knockoutValidation: 'knockout.validation.debug'
//        //jquery: '/Scripts/jquery-2.1.1'
//        //jquery: '//ajax.googleapis.com/ajax/libs/jquery/1.11.1/jquery.min.js'
//    },
//    //shim: {
//    //    'knockoutValidation': {
//    //        //These script dependencies should be loaded before loading
//    //        //backbone.js
//    //        deps: ['knockout'],
//    //        //Once loaded, use the global 'Backbone' as the
//    //        //module value.
//    //        exports: 'KnockoutValidation'
//    //    }
//    //}
//});

require.config({
    baseUrl: "/Scripts",
    paths: {
        "knockout": "knockout-3.2.0.debug",
        "knockout.validation": "knockout.validation",
        knockoutMapper: 'knockout.mapping-latest.debug'
    },
    //shim: {
    //    "knockout.validation": {
    //        "deps": ["knockout"]
    //    }
    //}
});

require(['src/html5Upload', 'domReady', 'knockout-models'], function (html5Upload, domReady, models) {
    'use strict';
    
    domReady(function () {
        var context = document.getElementById('page-viewmodel');

        var pageViewModel = new models.PageViewModel("newsarticleimages");
		
        models.Initialize(pageViewModel);

        models.applyBindings(pageViewModel, context);
    });
});
