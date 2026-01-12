using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum PaymentType
    {
        CustomerReceipt = 1, // Money Coming In (AR -> Cash)
        SupplierPayment = 2  // Money Going Out (Cash -> AP)
    }

    public enum PaymentMethod
    {
        Cash = 1,
        BankTransfer = 2,
        Cheque = 3
    }
}
