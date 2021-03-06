﻿using System;
using System.Linq;
using Merchello.Core.Models;
using Merchello.Core.Services;
using Merchello.Plugin.Payments.PayPal.Models;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;

namespace Merchello.Plugin.Payments.PayPal
{
	public class PayPalEvents : ApplicationEventHandler
	{
		protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication,
										   ApplicationContext applicationContext)
		{
			base.ApplicationStarted(umbracoApplication, applicationContext);

			LogHelper.Info<PayPalEvents>("Initializing PayPal provider registration binding events");


			GatewayProviderService.Saving += delegate(IGatewayProviderService sender, SaveEventArgs<IGatewayProviderSettings> args)
			{
				var key = new Guid(Constants.PayPalPaymentGatewayProviderKey);
				var provider = args.SavedEntities.FirstOrDefault(x => key == x.Key && !x.HasIdentity);
				if (provider == null) return;

				provider.ExtendedData.SaveProcessorSettings(new PayPalProcessorSettings());

			};
		}
	}
}
