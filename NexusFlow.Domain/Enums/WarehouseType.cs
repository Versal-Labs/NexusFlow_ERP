using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum WarehouseType
    {
        Internal = 0,         // Your own physical warehouse
        Subcontractor = 1,    // A 3rd party factory (Garment/Washing/Dyeing)
        Transit = 2           // Virtual location for goods in transit
    }
}
