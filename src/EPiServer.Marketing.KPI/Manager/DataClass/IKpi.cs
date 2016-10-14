﻿using System;
using EPiServer.Marketing.KPI.Common;
using EPiServer.Marketing.KPI.Results;
using System.Collections.Generic;

namespace EPiServer.Marketing.KPI.Manager.DataClass
{
    /// <summary>
    /// Interface for KPI objects.
    /// </summary>
    public interface IKpi
    {
        /// <summary>
        /// Id of Kpi.
        /// </summary>
        Guid Id { get; set; }

        /// <summary>
        /// Name displayed in the UI, default displays class type name
        /// </summary>
        string FriendlyName { get; }

        /// <summary>
        /// Optional description for the UI
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Markup used for configuration UI
        /// </summary>
        string UiMarkup { get; }

        /// <summary>
        /// Markup to use for read only ui
        /// </summary>
        string UiReadOnlyMarkup { get; }

        /// <summary>
        /// Date the kpi was created.
        /// </summary>
        DateTime CreatedDate { get; set; }

        /// <summary>
        /// The last time the kpi was modified.
        /// </summary>
        DateTime ModifiedDate { get; set; }

        /// <summary>
        /// Provides specific validation of data prior to creating the KPI
        /// </summary>
        /// <returns></returns>
        bool Validate(Dictionary<string,string> kpiData);

        /// <summary>
        /// Determines if a conversion has happened.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event Argument</param>
        IKpiResult Evaluate(object sender, EventArgs e);
    }
}
