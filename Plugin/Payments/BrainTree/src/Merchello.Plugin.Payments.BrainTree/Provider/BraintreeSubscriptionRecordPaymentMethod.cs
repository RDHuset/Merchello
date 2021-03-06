﻿namespace Merchello.Plugin.Payments.Braintree.Provider
{
    using System;
    using System.Linq;

    using global::Braintree;

    using Merchello.Core;
    using Merchello.Core.Gateways;
    using Merchello.Core.Gateways.Payment;
    using Merchello.Core.Models;
    using Merchello.Core.Models.TypeFields;
    using Merchello.Core.Services;
    using Merchello.Plugin.Payments.Braintree.Services;

    using Newtonsoft.Json;

    using Umbraco.Core;

    using Constants = Merchello.Plugin.Payments.Braintree.Constants;

    /// <summary>
    /// The braintree subscription record payment method.
    /// </summary>
    [GatewayMethodUi("BrainTree.SubscriptionRecordTransaction")]
    [GatewayMethodEditor("BrainTree Payment Method Editor", "~/App_Plugins/Merchello/Backoffice/Merchello/Dialogs/payment.paymentmethod.addedit.html")]
    [PaymentGatewayMethod("Braintree Payment Gateway Method Editors",
   "~/App_Plugins/Merchello.Braintree/vault/payment.vault.authorizecapturepayment.html",
   "~/App_Plugins/Merchello.Braintree/vault/payment.vault.voidpayment.html",
   "~/App_Plugins/Merchello.Braintree/vault/payment.vault.refundpayment.html", 
   false)]
    public class BraintreeSubscriptionRecordPaymentMethod : PaymentGatewayMethodBase, IBraintreeSubscriptionRecordPaymentGatewayMethod
    {
        /// <summary>
        /// The _braintree api service.
        /// </summary>
        private IBraintreeApiService _braintreeApiService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BraintreeSubscriptionRecordPaymentMethod"/> class.
        /// </summary>
        /// <param name="gatewayProviderService">
        /// The gateway provider service.
        /// </param>
        /// <param name="paymentMethod">
        /// The payment method.
        /// </param>
        /// <param name="braintreeApiService">The <see cref="IBraintreeApiService"/></param>
        public BraintreeSubscriptionRecordPaymentMethod(IGatewayProviderService gatewayProviderService, IPaymentMethod paymentMethod, IBraintreeApiService braintreeApiService)
            : base(gatewayProviderService, paymentMethod)
        {
            Mandate.ParameterNotNull(braintreeApiService, "braintreeApiService");

            _braintreeApiService = braintreeApiService;
        }

        /// <summary>
        /// The perform authorize payment.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        protected override IPaymentResult PerformAuthorizePayment(IInvoice invoice, ProcessorArgumentCollection args)
        {
            var payment = GatewayProviderService.CreatePayment(PaymentMethodType.CreditCard, invoice.Total, PaymentMethod.Key);
            payment.CustomerKey = invoice.CustomerKey;
            payment.PaymentMethodName = PaymentMethod.Name;
            payment.ReferenceNumber = PaymentMethod.PaymentCode + "-" + invoice.PrefixedInvoiceNumber();
            payment.Collected = false;
            payment.Authorized = true;

            GatewayProviderService.Save(payment);

            // In this case, we want to do our own Apply Payment operation as the amount has not been collected -
            // so we create an applied payment with a 0 amount.  Once the payment has been "collected", another Applied Payment record will
            // be created showing the full amount and the invoice status will be set to Paid.
            GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Debit, string.Format("To show promise of a {0} payment", PaymentMethod.Name), 0);

            return new PaymentResult(Attempt.Succeed(payment), invoice, false);
        }

        /// <summary>
        /// The perform authorize capture payment.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="amount">
        /// The amount.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        protected override IPaymentResult PerformAuthorizeCapturePayment(IInvoice invoice, decimal amount, ProcessorArgumentCollection args)
        {
            var payment = GatewayProviderService.CreatePayment(PaymentMethodType.Cash, amount, PaymentMethod.Key);
            payment.CustomerKey = invoice.CustomerKey;
            payment.PaymentMethodName = PaymentMethod.Name + " " + PaymentMethod.PaymentCode;
            payment.ReferenceNumber = PaymentMethod.PaymentCode + "-" + invoice.PrefixedInvoiceNumber();
            payment.Collected = true;
            payment.Authorized = true;

            string transaction;
            if (args.TryGetValue(Constants.ExtendedDataKeys.BraintreeTransaction, out transaction))
            {
                payment.ExtendedData.SetValue(Constants.ExtendedDataKeys.BraintreeTransaction, transaction);
            }

            GatewayProviderService.Save(payment);

            GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Debit, "Braintree subscription payment", amount);

            return new PaymentResult(Attempt<IPayment>.Succeed(payment), invoice, CalculateTotalOwed(invoice).CompareTo(amount) <= 0);
        }

        /// <summary>
        /// The perform capture payment.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="payment">
        /// The payment.
        /// </param>
        /// <param name="amount">
        /// The amount.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        protected override IPaymentResult PerformCapturePayment(IInvoice invoice, IPayment payment, decimal amount, ProcessorArgumentCollection args)
        {
            payment.Collected = true;
            payment.Authorized = true;

            string transaction;
            if (args.TryGetValue(Constants.ExtendedDataKeys.BraintreeTransaction, out transaction))
            {
                payment.ExtendedData.SetValue(Constants.ExtendedDataKeys.BraintreeTransaction, transaction);
            }

            GatewayProviderService.Save(payment);

            GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Debit, "Braintree subscription payment", amount);

            return new PaymentResult(Attempt<IPayment>.Succeed(payment), invoice, CalculateTotalOwed(invoice).CompareTo(amount) <= 0);
        }

        /// <summary>
        /// The perform refund payment.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="payment">
        /// The payment.
        /// </param>
        /// <param name="amount">
        /// The amount.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        protected override IPaymentResult PerformRefundPayment(IInvoice invoice, IPayment payment, decimal amount, ProcessorArgumentCollection args)
        {
            foreach (var applied in payment.AppliedPayments())
            {
                applied.TransactionType = AppliedPaymentType.Refund;
                applied.Amount = 0;
                applied.Description += " - Refunded";
                GatewayProviderService.Save(applied);
            }

            payment.Amount = payment.Amount - amount;

            if (payment.Amount != 0)
            {
                GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Debit, "To show partial payment remaining after refund", payment.Amount);
            }

            GatewayProviderService.Save(payment);

            return new PaymentResult(Attempt<IPayment>.Succeed(payment), invoice, false);
        }

        /// <summary>
        /// The perform void payment.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="payment">
        /// The payment.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        protected override IPaymentResult PerformVoidPayment(IInvoice invoice, IPayment payment, ProcessorArgumentCollection args)
        {
            string transactionString;
            if (args.TryGetValue(Constants.ExtendedDataKeys.BraintreeTransaction, out transactionString))
            {
                var transaction = JsonConvert.DeserializeObject<Transaction>(transactionString);
                var result = _braintreeApiService.Transaction.Refund(transaction.Id);

                if (!result.IsSuccess()) return new PaymentResult(Attempt<IPayment>.Fail(payment), invoice, false);  

                foreach (var applied in payment.AppliedPayments())
                {
                    applied.TransactionType = AppliedPaymentType.Void;
                    applied.Amount = 0;
                    applied.Description += " - **Void**";
                    GatewayProviderService.Save(applied);
                }

                payment.Voided = true;
                GatewayProviderService.Save(payment);

                return new PaymentResult(Attempt<IPayment>.Succeed(payment), invoice, false);
            }
            
            return new PaymentResult(Attempt<IPayment>.Fail(payment), invoice, false);
        }

        /// <summary>
        /// The calculate total owed.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <returns>
        /// The <see cref="decimal"/>.
        /// </returns>
        private decimal CalculateTotalOwed(IInvoice invoice)
        {
            var applied = invoice.AppliedPayments(GatewayProviderService).ToArray();

            var owed =
                applied.Where(
                    x =>
                    x.AppliedPaymentTfKey.Equals(
                        EnumTypeFieldConverter.AppliedPayment.GetTypeField(AppliedPaymentType.Debit).TypeKey))
                    .Select(y => y.Amount)
                    .Sum()
                - applied.Where(
                    x =>
                    x.AppliedPaymentTfKey.Equals(
                        EnumTypeFieldConverter.AppliedPayment.GetTypeField(AppliedPaymentType.Credit).TypeKey))
                      .Select(y => y.Amount)
                      .Sum();

            return owed;
        }
    }
}