using Microsoft.AspNetCore.Mvc.Rendering;

namespace ZnErp.Models;

public class DocMeta
{
    public string Title { get; set; } = "";
    public string Prefix { get; set; } = "";
    public bool NeedsOther { get; set; }
    public string OtherLabel { get; set; } = "";
    public string Hint { get; set; } = "";
}

public class LineItemOption
{
    public string Value { get; set; } = "";
    public string Text { get; set; } = "";
    public decimal DefaultAmount { get; set; }
}

public class LineRow
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}

public class LineEditorModel
{
    public string AmountLabel { get; set; } = "Rate";
    public string AmountField { get; set; } = "Rate";
    public List<LineItemOption> Items { get; set; } = new();
    public List<LineRow> Rows { get; set; } = new();
}

public class ReportViewModel
{
    public string Title { get; set; } = "";

    public bool ShowFrom { get; set; }
    public string FromLabel { get; set; } = "From";
    public bool ShowTo { get; set; }
    public bool ShowText { get; set; }
    public string TextLabel { get; set; } = "";

    public DateTime From { get; set; } = DateTime.Today;
    public DateTime To { get; set; } = DateTime.Today;
    public string? Q { get; set; }
    public int? StoreId { get; set; }
    public int? AccountId { get; set; }
    public int? CustomerId { get; set; }
    /// <summary>Report se wapas jane ka link (jaise Primary Sales list).</summary>
    public string? BackUrl { get; set; }
    public string? Back { get; set; }
    public List<SelectListItem>? Stores { get; set; }
    public List<SelectListItem>? Accounts { get; set; }
    public List<SelectListItem>? Customers { get; set; }

    public List<string> Columns { get; set; } = new();
    public List<string[]> Rows { get; set; } = new();
    public string[]? Footer { get; set; }
    public List<string> Notes { get; set; } = new();
}
