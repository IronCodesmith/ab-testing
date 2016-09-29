﻿define([
     'dojo/_base/declare',
        'dijit/_WidgetBase',
        'dijit/_TemplatedMixin',
        'dojo/text!marketing-testing/views/AddTestView.html',
        'epi/i18n!marketing-testing/nls/abtesting',
        'marketing-testing/viewmodels/AddTestViewModel',
        'dijit/_WidgetsInTemplateMixin',
        'epi/shell/widget/_ModelBindingMixin',
        'epi/datetime',
        'epi/username',
        'dojo/topic',
        'dojo/html',
        'dojo/dom',
        "dojo/dom-class",
        "dojo/query",
        "dijit/registry",
        'epi/dependency',
        "marketing-testing/scripts/rasterizeHTML",
        'xstyle/css!marketing-testing/css/ABTesting.css',
        'xstyle/css!marketing-testing/css/GridForm.css',
        'xstyle/css!marketing-testing/css/dijit.css',
        'dijit/form/Button',
        'dijit/form/NumberSpinner',
        'dijit/form/Textarea',
        'dijit/form/RadioButton',
        'epi-cms/widget/ContentSelector',
        'epi/shell/widget/DateTimeSelectorDropDown',
        'dijit/form/TextBox',
        'epi-cms/widget/Breadcrumb',
        "dijit/layout/AccordionContainer",
        "dijit/layout/ContentPane"
],
    function (
    declare,
    _WidgetBase,
    _TemplatedMixin,
    template,
    resources,
    AddTestViewModel,
    _WidgetsInTemplateMixin,
    _ModelBindingMixing,
    datetime,
    username,
    topic,
    html,
    dom,
    domClass,
    query,
    registry,
    dependency,
    rasterizehtml
) {
        viewPublishedVersion: null;
        viewCurrentVersion: null;
        viewParticipationPercent: null;
        viewTestDuration: null;
        viewConfidenceLevel: null;

        return declare([_WidgetBase, _TemplatedMixin, _WidgetsInTemplateMixin, _ModelBindingMixing],
        {
            templateString: template,

            resources: resources,

            //set bindings to view model properties
            modelBindingMap: {
                publishedVersion: ["viewPublishedVersion"],
                currentVersion: ["viewCurrentVersion"],
                participationPercent: ["viewParticipationPercent"],
                testDuration: ["viewTestDuration"],
                confidenceLevel: ["viewConfidenceLevel"]
            },

            //sets views starting data from view model
            postMixInProperties: function () {
                this.model = this.model || new AddTestViewModel({ contentData: this.contentData });
                this._contextChangedHandler = dojo.subscribe('/epi/marketing/updatestate', this, this._onContextChange);
            },

            _onContextChange: function (context, caller) {
                this.contentData = caller.contentData;
                this.reset();
            },

            //sets default values once everything is loaded
            postCreate: function () {
                this.reset();
                this.inherited(arguments);

            },

            startup: function () {
                if (this.breadcrumbWidget) {
                    this.breadcrumbWidget.set("contentLink", this.contentData.contentLink);
                    this.contentNameNode.innerText = this.contentData.name;
                    this.breadcrumbWidget._addResizeListener();
                    this.breadcrumbWidget.layout();
                }
            },

            reset: function () {
                //set view model properties to default form values.
                if (this.descriptionText) {
                    this.descriptionText.value = this.model.testDescription = "";
                }

                if (this.participationPercentText) {
                    this.participationPercentText.reset();
                    this.model.participationPercent = this.participationPercentText.value;
                }

                if (this.durationText) {
                    this.durationText.reset();
                    this.model.testDuration = this.durationText.value;
                }

                if (this.startDatePicker) {
                    this.startDatePicker.reset();
                    this.model.startDate = new Date(Date.now()).toUTCString();
                }

                if (this.breadcrumbWidget) {
                    this.breadcrumbWidget.set("contentLink", this.contentData.contentLink);
                    this.contentNameNode.innerText = this.contentData.name;
                    this.breadcrumbWidget.layout();
                }

                if (this.conversionPageWidget) {
                    this.conversionPageWidget.reset();
                }

                if (this.start) {
                    this.start.set("checked", true);
                }

                if (this.delay) {
                    this.delay.set("checked", false);
                }

                if (this.scheduleDiv) {
                    this.scheduleDiv.style.visibility = "hidden";
                }

                if (dom.byId("confidence")) {
                    dom.byId("confidence").value = this.confidenceLevel.value;
                }

                this._setViewPublishedVersionAttr(true);
                this._setViewCurrentVersionAttr();
                this._clearConversionErrors();
                this._setViewConfidenceLevelAttr();
            },

            //setters for bound properties
            _setViewPublishedVersionAttr: function (viewPublishedVersion) {
                //do dom stuff
                if (!viewPublishedVersion) {
                    return;
                }
                this.publishedBy.textContent = username.toUserFriendlyString(this.contentData.publishedBy);
                this.datePublished.textContent = datetime.toUserFriendlyString(this.contentData.lastPublished);
                this.model.testContentId = this.contentData.contentGuid;
                var pubThumb = document.getElementById("publishThumbnail");

                if (pubThumb) {
                    //Hack to build published versions preview link below
                    var isCatalogContent = this.contentData.previewUrl.toLowerCase().indexOf('catalogcontent') !== -1;
                    var publishContentVersion = this.model.publishedVersion.contentLink.split('_'),
                        previewUrlEnd = isCatalogContent ? publishContentVersion[1] + '_CatalogContent' + '/?epieditmode=False' : publishContentVersion[1] + '/?epieditmode=False',
                        previewUrlStart = this.contentData.previewUrl.split('_'),
                        previewUrl = previewUrlStart[0] + '_' + previewUrlEnd;
                    pubThumb.height = 768;
                    pubThumb.width = 1024;
                    rasterizehtml.drawURL(previewUrl, pubThumb, { height: 768, width: 1024 }).then(function success(renderResult) {
                        query('.versiona').addClass('hide-bg');
                    });
                }
            },

            _setViewCurrentVersionAttr: function () {
                if (!this.contentData) {
                    return;
                }
                this.savedBy.textContent = username.toUserFriendlyString(this.contentData.changedBy);
                this.dateSaved.textContent = datetime.toUserFriendlyString(this.contentData.saved);
                this.pageName.textContent = this.contentData.name + " A/B Test";

                var pubThumb = document.getElementById("draftThumbnail");

                if (pubThumb) {
                    var previewUrl = this.model.contentData.previewUrl;

                    pubThumb.height = 768;
                    pubThumb.width = 1024;
                    rasterizehtml.drawURL(previewUrl, pubThumb, { height: 768, width: 1024 }).then(function success(renderResult) {
                        query('.versionb').addClass('hide-bg');
                    });
                }
            },

            _setViewParticipationPercentAttr: function (viewParticipationPercent) {
                this.participationPercentText.set("Value", viewParticipationPercent);
            },

            _setViewTestDurationAttr: function (viewTestDuration) {
                this.durationText.set("Value", viewTestDuration);
            },

            _setViewConfidenceLevelAttr: function (viewConfidenceLevel) {
                var rbs = ["confidence_99", "confidence_98", "confidence_95", "confidence_90"];
                this._resetConfidenceSelection(rbs);
                for (var i = 0; i < rbs.length; i++) {
                    var rb = dom.byId(rbs[i]);
                    if (!rb) {
                        return;
                    } else if (rb.value === viewConfidenceLevel.toString()) {
                        rb.setAttribute("selected", "selected");
                        rb.textContent = rb.value + "% " + resources.addtestview.default;
                    } else {
                        rb.removeAttribute("selected");
                        rb.textContent = rb.value + "%";
                    }
                }
            },

            _resetConfidenceSelection: function (rbs) {
                for (var i = 0; i < rbs.length; i++) {
                    var rb = dom.byId(rbs[i]);
                    if (!rb) {
                        return;
                    } else if (rb.textContent.indexOf("(default)") != -1) {
                        rb.selected = true;
                    } else {
                        rb.selected = false;
                    }
                }
            },

            _clearConversionErrors: function () {
                var errorText = dom.byId("pickerErrorText");

                if (!errorText) {
                    return;
                }

                errorText.innerText = "";
                errorText.style.visibility = "hidden";
                var et2 = dom.byId("pickerErrorIcon");
                et2.style.visibility = "hidden";
            },

            _getConfidenceLevel: function () {
                var rbs = ["confidence_99", "confidence_98", "confidence_95", "confidence_90"];
                for (i = 0; i < rbs.length; i++) {
                    var rb = dom.byId(rbs[i]);
                    if (!rb) {
                        return;
                    } else if (rb.checked) {
                        this.model.confidencelevel = rb.value;
                        return;
                    }
                }
            },

            // Master validations for all form fields. Used when hitting the start button and will pickup
            // errors not triggered by onChangeEvents.
            _isValidFormData: function () {
                return this._isValidConversionPage() & this._isValidPercentParticipation() &
                    this._isValidDuration() & this._isValidStartDate();
            },

            //Validates a value has been selected for the conversion page.
            _isValidConversionPage: function () {
                var errorTextNode = dom.byId("pickerErrorText");
                var errorIconNode = dom.byId("pickerErrorIcon");
                var conversionPage = this.conversionPageWidget.value;

                if (!conversionPage) {
                    this._setError(resources.addtestview.error_conversiongoal, errorTextNode, errorIconNode);
                    return false;
                }

                this._setError("", errorTextNode, errorIconNode);
                return true;
            },

            //Validates the participation percent entered is between 1 and 100
            _isValidPercentParticipation: function () {
                var errorTextNode = dom.byId("participationErrorText");
                var errorIconNode = dom.byId("participationErrorIcon");
                var participationPercentage = dom.byId("percentageSpinner").value;

                if (!this._isUnsignedNumeric(participationPercentage) || participationPercentage <= 0 || participationPercentage > 100) {
                    this._setError(resources.addtestview.error_participation_percentage, errorTextNode, errorIconNode);
                    return false;
                }

                this._setError("", errorTextNode, errorIconNode);
                return true;
            },

            //Validates the duration entered is not less than 1 day
            _isValidDuration: function () {
                var errorTextNode = dom.byId("durationErrorText");
                var errorIconNode = dom.byId("durationErrorIcon");
                var duration = dom.byId("durationSpinner").value;

                if (!this._isUnsignedNumeric(duration) || duration < 1 || duration > 365) {
                    this._setError(resources.addtestview.error_duration, errorTextNode, errorIconNode);
                    return false;
                }

                this._setError("", errorTextNode, errorIconNode);
                return true;
            },

            //Validates the date information is A) a valid date format and B) not in the past
            _isValidStartDate: function () {
                var errorTextNode = dom.byId("datePickerErrorText");
                var errorIconNode = dom.byId("datePickerErrorIcon");
                var scheduleText = dom.byId("ScheduleText");
                var dateValue = dom.byId("StartDateTimeSelector").value;
                var now = new Date();

                if (dateValue !== "") {
                    if (isNaN(new Date(dateValue))) {
                        this._setError(resources.addtestview.error_invalid_date_time_value, errorTextNode, errorIconNode);
                        scheduleText.innerText = resources.addtestview.error_test_not_schedulded_or_started;
                        return false;
                    } else if (new Date(dateValue) < now) {
                        this._setError(resources.addtestview.error_date_in_the_past, errorTextNode, errorIconNode);
                        scheduleText.innerText = resources.addtestview.error_test_not_schedulded_or_started;
                        return false;
                    }
                }

                this._setError("", errorTextNode, errorIconNode);
                return true;
            },

            _isUnsignedNumeric: function (string) {
                if (string.match(/^[0-9]+$/) == null) {
                    return false;
                }
                return true;
            },

            //Toggles an errors text content and icon visibitlity based on the error and nodes supplied
            _setError: function (errorText, errorNode, iconNode) {
                if (errorText) {
                    errorNode.innerText = errorText;
                    errorNode.style.visibility = "visible";
                    iconNode.style.visibility = "visible";
                } else {
                    errorNode.innerText = "";
                    errorNode.style.visibility = "hidden";
                    iconNode.style.visibility = "hidden";
                }
            },

            //EVENT HANDLERS
            //Start and Cancel Events

            _onStartButtonClick: function () {
                
                this.model.testDescription = dom.byId("testDescription").value;
                var startDateSelector = dom.byId("StartDateTimeSelector");
                var utcNow = new Date(Date.now()).toUTCString();
                if (startDateSelector.value === "") {
                    this.model.startDate = utcNow;
                }

                this.model.confidencelevel = dom.byId("confidence").value;
                this.model.testTitle = this.pageName.textContent;

                if (this._isValidFormData()) {
                    var startButton = registry.byId("StartButton");
                    startButton.setDisabled(true);
                    this._contentVersionStore = this._contentVersionStore || epi.dependency.resolve("epi.storeregistry").get("epi.cms.contentversion");
                    this._contentVersionStore
                        .query({ contentLink: this.model.conversionPage, language: this.languageContext ? this.languageContext.language : "", query: "getpublishedversion" })
                        .then(function (result) {
                            var errorTextNode = dom.byId("pickerErrorText");
                            var errorIconNode = dom.byId("pickerErrorIcon");
                            if (result) {
                                if (result.contentLink !== this.model.publishedVersion.contentLink) {
                                    this.model.createTest();
                                } else {
                                    this._setError(resources.addtestview.error_selected_samepage, errorTextNode, errorIconNode);
                                }
                            } else {
                                this._setError(resources.addtestview.error_selected_notpublished, errorTextNode, errorIconNode);
                            }
                        }.bind(this))
                        .otherwise(function (result) {
                            console.log("Query failed, we cannot tell if this page is a valid page or not.");
                        });
                    startButton.setDisabled(false);
                }
            },

            _onCancelButtonClick: function () {
                var me = this;
                me.contextParameters = { uri: "epi.cms.contentdata:///" + this.model.publishedVersion.contentLink.split('_')[0] };
                topic.publish("/epi/shell/context/request", me.contextParameters);
            },

            // Form Field Events
            _onTestDescriptionChanged: function (event) {
                this.model.testDescription = event;
            },

            _onConversionPageChanged: function (event) {
                if (this._isValidConversionPage()) {
                    this.model.conversionPage = event;
                }
            },

            _onPercentageSpinnerChanged: function (event) {
                if (this._isValidPercentParticipation()) {
                    this.model.participationPercent = event;
                }
            },

            _onDurationSpinnerChanged: function (event) {
                if (this._isValidDuration()) {
                    this.model.testDuration = event;
                }
            },

            _onDateTimeChange: function (event) {
                var startButton = registry.byId("StartButton");
                var scheduleText = dom.byId("ScheduleText");
                var startDateSelector = dom.byId("StartDateTimeSelector");

                if (event === null) {
                    event = startDateSelector.value;
                }

                if (this._isValidStartDate(event)) {
                    if (event !== "") {
                        var localDate = new Date(event).toLocaleDateString();
                        var localTime = new Date(event).toLocaleTimeString();
                        startButton.set("label", resources.addtestview.schedule_test);
                        scheduleText.innerText = resources.addtestview.schedule_tobegin_on + localDate + "," + localTime;
                        this.model.startDate = new Date(event).toUTCString();
                        this.model.start = false;
                    } else {
                        startButton.set("label", resources.addtestview.start_default);
                        scheduleText.innerText = resources.addtestview.notscheduled_text;
                        this.model.start = true;
                    }
                }
            },

            _toggleTimeSelector: function () {
                var dateSelector = dom.byId("dateSelector");

                if (dateSelector.style.visibility === "hidden") {
                    dateSelector.style.visibility = "visible";
                } else {
                    this.startDatePicker.reset();
                    dateSelector.style.visibility = "hidden";
                }
            }
        });
    });