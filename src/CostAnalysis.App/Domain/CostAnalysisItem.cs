namespace CostAnalysis.App.Domain
{
    internal sealed class CostAnalysisItem
    {
        public int No { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string MaterialDescription { get; set; }
        public string Supplier { get; set; }
        public string BaseMaterialName { get; set; }
        public string MaterialVendor { get; set; }
        public decimal? MaterialUnitPrice { get; set; }
        public string GramWeight { get; set; }
        public string ExpandedSize { get; set; }
        public decimal? MaterialCost { get; set; }
        public decimal? PrintingCost { get; set; }
        public decimal? PostProcessCost { get; set; }
        public decimal? OtherCost { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public decimal? TotalQuantity { get; set; }

        public decimal? TotalPrice
        {
            get
            {
                if (!PurchaseUnitPrice.HasValue || !TotalQuantity.HasValue)
                {
                    return null;
                }

                return PurchaseUnitPrice.Value * TotalQuantity.Value;
            }
        }
    }
}
