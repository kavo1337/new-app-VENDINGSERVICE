using System;
using System.Collections.Generic;

namespace app.API.Data.Entity;

public partial class SalePaymentMethod
{
    public int SalePaymentMethodId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Sale> Sale { get; set; } = new List<Sale>();
}
