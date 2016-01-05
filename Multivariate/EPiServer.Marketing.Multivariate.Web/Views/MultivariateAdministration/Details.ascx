<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<IList<IMultivariateTest>>" %>
<%@ Import Namespace="EPiServer.Marketing.Multivariate.Model" %>
<%@ Import Namespace="EPiServer.Marketing.Multivariate.Web.Helpers" %>
<%@ Import Namespace="EPiServer.Shell.Web.Mvc.Html"%>
<%@ Import Namespace="EPiServer.Core" %>

<html>
<head>
    <meta name="viewport" content="width=device-width" />
    <title></title>

        <%=Page.ClientResources("ShellCore")%>
        <%=Page.ClientResources("ShellCoreLightTheme")%>
        <%= Html.ScriptResource(EPiServer.Shell.Paths.ToClientResource("CMS", "ClientResources/BrokenLinks/BrokenLinks.js"))%>
        <%= Html.CssLink(EPiServer.Shell.Paths.ToClientResource("CMS", "ClientResources/BrokenLinks/BrokenLinks.css"))%>
        <%= Html.CssLink(EPiServer.Web.PageExtensions.ThemeUtility.GetCssThemeUrl(Page, "system.css"))%>
        <%= Html.CssLink(EPiServer.Web.PageExtensions.ThemeUtility.GetCssThemeUrl(Page, "ToolButton.css"))%>

</head>
<body class="sleek">

<asp:Content>
<div class="epi-contentContainer epi-padding" >
<div class="epi-contentArea" >
<%= Html.ViewLinkButton(LanguageManager.Instance.Translate("/multivariate/settings/back"), LanguageManager.Instance.Translate("/multivariate/gadget/back"), "Index/?id=1&",  "", "", null)%>
<h1 class="EP-prefix"><%= LanguageManager.Instance.Translate("/multivariate/settings/details")%></h1>
<table class="epi-default">
<tr><th>Name</th><th>Owner</th><th>State</th><th>Start</th><th>End</th></tr>
<%  	
	UIHelper helper = new UIHelper();
	foreach (var item in Model) { 
%>
	<tr><td><%= item.Title%></td><td><%= item.Owner%></td><td><%= item.TestState%></td><td><%= item.StartDate%></td><td><%= item.EndDate%></tr>
</table>
</div>
<div class="epi-contentArea" >
<h1><%= LanguageManager.Instance.Translate("/multivariate/settings/results")%></h1>
<table>
<tr><th>Item name</th><th>Views</th><th>Conversions</th><th>Conversion Rate</th></tr>
<%
	foreach( var result in item.MultivariateTestResults ) {
		int rate = 0;
		if( result.Views != 0 )
			rate = (int)(result.Conversions * 100.0 / result.Views);
%>
	<tr><td><%= helper.getContent( result.ItemId ).Name %></td><td><%= result.Views %></td><td><%= result.Conversions %></td><td><%= rate %> %</td></tr>
	
	<% } %>
</table>
</div>

<% } %>

</div>
</asp:Content>
</body>
</html>