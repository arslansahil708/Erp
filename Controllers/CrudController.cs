using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZnErp.Data;

namespace ZnErp.Controllers;

/// <summary>
/// Generic list/create/edit/delete controller. Har master screen (Stores, Items, Accounts ...)
/// isse inherit karti hai aur shared views CrudIndex / CrudForm use karti hai.
/// </summary>
public abstract class CrudController<T> : Controller where T : class, new()
{
    private const string IndexView = "~/Views/Shared/CrudIndex.cshtml";
    private const string FormView = "~/Views/Shared/CrudForm.cshtml";

    protected readonly AppDbContext Db;
    protected readonly Dictionary<string, List<SelectListItem>> Lookups = new();

    protected CrudController(AppDbContext db) => Db = db;

    protected abstract string Title { get; }
    protected virtual bool AllowDelete => true;

    protected virtual IQueryable<T> Query() =>
        Db.Set<T>().AsNoTracking().OrderByDescending(e => EF.Property<int>(e, "Id"));

    protected virtual Task LoadLookups() => Task.CompletedTask;
    protected virtual Task BeforeSave(T entity, bool isNew) => Task.CompletedTask;
    protected virtual Task AfterSave(T entity, bool isNew) => Task.CompletedTask;
    protected virtual Task BeforeDelete(T entity) => Task.CompletedTask;

    private async Task Prepare(string mode)
    {
        await LoadLookups();
        ViewBag.EntityType = typeof(T);
        ViewBag.PageTitle = Title;
        ViewBag.Lookups = Lookups;
        ViewBag.AllowDelete = AllowDelete;
        ViewBag.Mode = mode;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await Prepare("index");
        return View(IndexView, await Query().ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await Prepare("create");
        return View(FormView, new T());
    }

    [HttpPost]
    public async Task<IActionResult> Create(T model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await BeforeSave(model, true);
                Db.Add(model);
                await Db.SaveChangesAsync();
                await AfterSave(model, true);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Save nahi ho saka. Code pehle se maujood hai ya reference ghalat hai.");
            }
        }

        await Prepare("create");
        return View(FormView, model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await Db.Set<T>().FindAsync(id);
        if (entity == null) return NotFound();

        await Prepare("edit");
        return View(FormView, entity);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, T model)
    {
        typeof(T).GetProperty("Id")!.SetValue(model, id);

        if (ModelState.IsValid)
        {
            try
            {
                await BeforeSave(model, false);
                Db.Update(model);
                await Db.SaveChangesAsync();
                await AfterSave(model, false);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Save nahi ho saka. Code pehle se maujood hai ya reference ghalat hai.");
            }
        }

        await Prepare("edit");
        return View(FormView, model);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!AllowDelete) return BadRequest();

        var entity = await Db.Set<T>().FindAsync(id);
        if (entity != null)
        {
            try
            {
                await BeforeDelete(entity);
                Db.Remove(entity);
                await Db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Yeh record kisi aur jagah use ho raha hai, isliye delete nahi ho sakta.";
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
