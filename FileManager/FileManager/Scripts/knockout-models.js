/*jslint browser:true*/
/*global define*/

define(['knockout', 'knockoutMapper'], function (ko, koMap) {
    'use strict';

    function trimTrailingZeros(number) {
        return number.toFixed(1).replace(/\.0+$/, '');
    }

    function formatFileSize(sizeInBytes) {
        var kiloByte = 1024,
            megaByte = Math.pow(kiloByte, 2),
            gigaByte = Math.pow(kiloByte, 3);

        if (sizeInBytes < kiloByte) {
            return sizeInBytes + ' B';
        }

        if (sizeInBytes < megaByte) {
            return trimTrailingZeros(sizeInBytes / kiloByte) + ' kB';
        }

        if (sizeInBytes < gigaByte) {
            return trimTrailingZeros(sizeInBytes / megaByte) + ' MB';
        }

        return trimTrailingZeros(sizeInBytes / gigaByte) + ' GB';
    }

    function ContainerBlobsViewModel(data, containerName) {
        var self = this;
        self.ContainerName = containerName;

        self.Mapping = {
            //'observe': [], // Covert the properties to non observable properties
            'Blobs': {
                create: function (options) {
                    return new BlobItemViewModel(options.data, self);
                }
            }
        };

        koMap.fromJS(data, self.Mapping, self);

        self.navigateToDirectory = function (directory) {
            // Get content of the selected directory
            var prefix;
            if (directory) {
                prefix = directory.PrefixFull();
            }

            $.getJSON("File/GetContainerBlobs",
                { containerName: self.ContainerName, prefix: prefix },
                function (data, textStatus, jqXHR) {
                    var vm = new ContainerBlobsViewModel(data, self.ContainerName);

                    self.Blobs(vm.Blobs());
                }
            );
        }

        self.DeleteFile = function (file) {
            // Delete the selected file
            $.post("File/DeleteFile", { containerName: self.ContainerName, fileName: file.Name, prefix: file.PrefixFull });

            self.Blobs.remove(file);
        }
    }

    function BlobItemViewModel(data, container) {
        var self = this;
        self.Container = container;

        koMap.fromJS(data, {}, self);
    }

    return {

        GetContainerBlobs: function (containerName) {
            $.getJSON("File/GetContainerBlobs",
                { containerName: containerName },
                function (data, textStatus, jqXHR) {
                    var vm = new ContainerBlobsViewModel(data, containerName);

                    var context2 = document.getElementById('blobList');
                    ko.applyBindings(vm, context2);
                }
            );
        },

        UploadsViewModel: function () {
            this.uploads = ko.observableArray([]);
            this.showTotalProgress = ko.observable();
            this.uploadSpeedFormatted = ko.observable();
            this.timeRemainingFormatted = ko.observable();
            this.totalProgress = ko.observable();
        },

        FileViewModel: function (file) {
            this.file = file;
            this.uploadProgress = ko.observable(0);
            this.uploadCompleted = ko.observable(false);
            this.uploadSpeedFormatted = ko.observable();
            this.fileName = file.fileName;
            this.fileSizeFormated = formatFileSize(file.fileSize);
        },

        applyBindings: function (model, context) {
            ko.applyBindings(model, context);
        }
    };

});
