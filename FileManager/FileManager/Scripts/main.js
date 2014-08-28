/*global require, alert*/
/*jslint browser:true*/

require.config({
    paths: {
        knockout: '/Scripts/knockout-3.1.0.debug',
        knockoutMapper: 'knockout.mapping-latest.debug',
        //jquery: '/Scripts/jquery-2.1.1'
        //jquery: '//ajax.googleapis.com/ajax/libs/jquery/1.11.1/jquery.min.js'
    }
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
