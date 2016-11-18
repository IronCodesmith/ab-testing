﻿using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Core;
using EPiServer.Framework.Localization;
using EPiServer.Marketing.KPI.Common.Attributes;
using EPiServer.Marketing.KPI.Exceptions;
using EPiServer.Marketing.KPI.Manager.DataClass;
using EPiServer.Marketing.KPI.Results;
using EPiServer.ServiceLocation;
using Mediachase.Commerce.Catalog;
using Mediachase.Commerce.Orders;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;

namespace EPiServer.Marketing.KPI.Commerce.Kpis
{
    [DataContract]
    [UIMarkup(configmarkup = "EPiServer.Marketing.KPI.Commerce.Markup.AddToCartConfigMarkup.html",
        readonlymarkup = "EPiServer.Marketing.KPI.Commerce.Markup.AddToCartReadOnlyMarkup.html",
        text = "Add To Cart", description = "Choose a product for conversion.")]
    public class AddToCartKpi : Kpi
    {
        IServiceLocator _servicelocator;

        [DataMember]
        public Guid ContentGuid;

        [DataMember]
        public bool isVariant;

        public AddToCartKpi()
        {
            _servicelocator = ServiceLocator.Current;
        }
        internal AddToCartKpi(IServiceLocator servicelocator)
        {
            _servicelocator = servicelocator;
        }

        public override void Validate(Dictionary<string, string> responseData)
        {
            if (responseData["ConversionProduct"] == "")
            {
                throw new KpiValidationException(LocalizationService.Current.GetString("/commercekpi/addtocart/config_markup/error_conversionproduct"));
            }

            //Get the currently configured content loader and reference converter from the service locator
            var contentLoader = _servicelocator.GetInstance<IContentLoader>();
            var referenceConverter = _servicelocator.GetInstance<ReferenceConverter>();

            //Get the correct product id as it's represented in EPiServer Commerce
            //In this example we arbitrarily use the integer 1
            var productIdFromCommerce = responseData["ConversionProduct"].Split('_')[0];

            var contentRepo = ServiceLocator.Current.GetInstance<IContentRepository>();
            var currentContent = contentRepo.Get<IContent>(new ContentReference(responseData["CurrentContent"]));

            //We use the content link builder to get the contentlink to our product
            var productLink = referenceConverter.GetContentLink(Int32.Parse(productIdFromCommerce), 
                CatalogContentType.CatalogEntry, 0);

            //Get the product using CMS API
            var content = contentLoader.Get<CatalogContentBase>(productLink);
            if (content.ContentType != CatalogContentType.CatalogEntry || !IsContentPublished(content))
            {
                throw new KpiValidationException(LocalizationService.Current.GetString("/commercekpi/addtocart/config_markup/error_not_published_product"));
            }
            ContentGuid = content.ContentGuid;
            isVariant = content is VariationContent;
        }

        private bool IsContentPublished(IContent content)
        {
            bool isPublished = true;
            IContentVersionRepository repo = _servicelocator.GetInstance<IContentVersionRepository>();
            var publishedContent = repo.LoadPublished(content.ContentLink);
            if (publishedContent == null)
            {
                isPublished = false;
            }
            return isPublished;
        }

        /// <summary>
        /// Called when we are expected to evaluate. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public override IKpiResult Evaluate(object sender, EventArgs e)
        {
            var retval = false;
                
            var contentLoader = _servicelocator.GetInstance<IContentLoader>();
            var referenceConverter = _servicelocator.GetInstance<ReferenceConverter>();

            var ea = e as OrderGroupEventArgs;
            var ordergroup = sender as OrderGroup;
            if (ea != null && ordergroup != null)
            {
                // todo, figure out how we convert based on what we have.
                // note that the order group has actual info in it, not the eventargs.
                //
                foreach (var o in ordergroup.OrderForms.ToArray())
                {
                    foreach( var lineitem in o.LineItems.ToArray())
                    {
                        
                        //We use the content link builder to get the contentlink to our product
                        var productLink = referenceConverter.GetContentLink(lineitem.Code);

                        //Get the product using CMS API
                        var productContent = contentLoader.Get<CatalogContentBase>(productLink);

                        //The commerce content name represents the name of the product
                        var productName = productContent.Name;

                        // if we are looking for an exact match at the entry level, 
                        // we can just check the Guid
                        if (isVariant)
                        {
                            retval = ContentGuid.Equals(productContent.ContentGuid);
                            if (retval)
                            {
                                break;
                            }
                        }
                        else
                        {
                            // else we can assume its a product variant
                            var repository = _servicelocator.GetInstance<IContentRepository>();
                            var variant = repository.Get<VariationContent>(productLink);
                            var parentProductRef = variant.GetParentProducts().FirstOrDefault();
                            if (parentProductRef != null)
                            {
                                //Get the parent product using CMS API
                                var parentProduct = contentLoader.Get<CatalogContentBase>(parentProductRef);
                                retval = ContentGuid.Equals(parentProduct.ContentGuid);
                                if (retval)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return new KpiConversionResult() { KpiId = Id, HasConverted = retval };
        }

        [DataMember]
        public override string UiMarkup
        {
            get
            {
                var conversionLabel = LocalizationService.Current
                    .GetString("/commercekpi/addtocart/config_markup/conversion_label");
                return string.Format(base.UiMarkup, conversionLabel);
            }
        }

        [DataMember]
        public override string UiReadOnlyMarkup
        {
            get
            {
                string markup = base.UiReadOnlyMarkup;

                var conversionHeaderText = LocalizationService.Current
                    .GetString("/commercekpi/addtocart/readonly_markup/conversion_header");
                var conversionDescription = LocalizationService.Current
                    .GetString("/commercekpi/addtocart/readonly_markup/conversion_selector_description");

                if (!Guid.Empty.Equals(ContentGuid))
                {
                    var contentRepository = _servicelocator.GetInstance<IContentRepository>();
                    var content = contentRepository.Get<IContent>(ContentGuid);
                    markup = string.Format(markup, conversionHeaderText, conversionDescription, 
                        content.ContentLink, content.Name);
                }

                return markup;
            }
        }

        private EventHandler<OrderGroupEventArgs> _eh;
        /// <summary>
        /// Setup the event that we want to be evaluated on
        /// </summary>
        public override event EventHandler EvaluateProxyEvent
        {
            add
            {
                _eh = new EventHandler<OrderGroupEventArgs>(value);
                OrderContext.Current.OrderGroupUpdated += _eh;
            }
            remove
            {
                OrderContext.Current.OrderGroupUpdated -= _eh;
            }
        }
    }
}