using System.Security.Claims;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Commands.ActualizarContacto;
using GuarinoApp.Application.Modules.Contactos.Commands.CrearContacto;
using GuarinoApp.Application.Modules.Contactos.Commands.EliminarContacto;
using GuarinoApp.Application.Modules.Contactos.Queries.GetContactos;
using GuarinoApp.Domain.Entities;
using GuarinoApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class ContactosWorkflowTests
{
    [TestMethod]
    public async Task Gerencia_CreaBuscaActualizaYEliminaContacto_ConAuditoriaYBorradoLogico()
    {
        await using var db = CreateDb();
        var audit = new RecordingAuditService();
        var user = new TestCurrentUserService("Gerencia");

        var create = await new CrearContactoCommandHandler(db, user, audit).Handle(
            new CrearContactoCommand("  Ana <b>Silva</b> ", "  Frutas del Sur  ", "UY", "ana@example.com", null, "Limones", "Primera reunión", ContactoEstados.Nuevo),
            CancellationToken.None);

        Assert.IsTrue(create.Success, create.Error);
        var contacto = await db.Contactos.SingleAsync();
        Assert.AreEqual("Ana Silva", contacto.Nombre);
        Assert.AreEqual("Frutas del Sur", contacto.Empresa);
        Assert.AreEqual("demo", contacto.CreatedBy);

        var query = new GetContactosQueryHandler(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<GetContactosQueryHandler>.Instance);
        var found = await query.Handle(new GetContactosQuery("Frutas", ContactoEstados.Nuevo), CancellationToken.None);
        Assert.AreEqual(1, found.Count);
        Assert.AreEqual(contacto.Id, found[0].Id);

        var update = await new ActualizarContactoCommandHandler(db, user, audit).Handle(
            new ActualizarContactoCommand(contacto.Id, "Ana Silva", "Frutas del Sur", "UY", "ana@example.com", null, "Naranjas", "Reunión realizada", ContactoEstados.EnSeguimiento),
            CancellationToken.None);

        Assert.IsTrue(update.Success, update.Error);
        Assert.AreEqual(ContactoEstados.EnSeguimiento, contacto.Estado);
        Assert.AreEqual("Reunión realizada", contacto.Notas);
        Assert.AreEqual("demo", contacto.UpdatedBy);

        var delete = await new EliminarContactoCommandHandler(db, user, audit).Handle(
            new EliminarContactoCommand(contacto.Id), CancellationToken.None);

        Assert.IsTrue(delete.Success, delete.Error);
        Assert.IsTrue(contacto.IsDeleted);
        Assert.AreEqual(0, (await query.Handle(new GetContactosQuery(), CancellationToken.None)).Count);
        CollectionAssert.AreEqual(new[] { "Create", "Update", "Delete" }, audit.Actions.ToArray());
    }

    [TestMethod]
    public async Task Operador_NoPuedeMutarContactos()
    {
        await using var db = CreateDb();
        var contacto = new Contacto { Nombre = "Comprador", Empresa = "Mercado", Estado = ContactoEstados.Nuevo };
        db.Contactos.Add(contacto);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditService();
        var operatorUser = new TestCurrentUserService("Operador");

        var create = await new CrearContactoCommandHandler(db, operatorUser, audit).Handle(
            new CrearContactoCommand("Otro", "Empresa", null, null, null, null, null, ContactoEstados.Nuevo), CancellationToken.None);
        var update = await new ActualizarContactoCommandHandler(db, operatorUser, audit).Handle(
            new ActualizarContactoCommand(contacto.Id, "Modificado", "Mercado", null, null, null, null, null, ContactoEstados.Nuevo), CancellationToken.None);
        var delete = await new EliminarContactoCommandHandler(db, operatorUser, audit).Handle(
            new EliminarContactoCommand(contacto.Id), CancellationToken.None);

        Assert.IsFalse(create.Success);
        Assert.IsFalse(update.Success);
        Assert.IsFalse(delete.Success);
        Assert.AreEqual("Comprador", contacto.Nombre);
        Assert.IsFalse(contacto.IsDeleted);
        Assert.AreEqual(0, audit.Actions.Count);
    }

    [TestMethod]
    public async Task AltaContacto_RechazaDatosInvalidos_SinPersistir()
    {
        await using var db = CreateDb();
        var result = await new CrearContactoCommandHandler(db, new TestCurrentUserService("Admin"), new RecordingAuditService()).Handle(
            new CrearContactoCommand("", "Empresa", null, "no-es-email", null, null, null, "estado-manipulado"), CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.Contactos.CountAsync());
    }

    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ContactosWorkflow-{Guid.NewGuid():N}")
            .Options);

    private sealed class TestCurrentUserService(string role) : ICurrentUserService
    {
        public string? UserId => "test-user-id";
        public string? UserName => "demo";
        public bool IsAuthenticated => true;
        public bool IsInRole(string requestedRole) => string.Equals(role, requestedRole, StringComparison.OrdinalIgnoreCase);
        public IEnumerable<Claim> Claims => new[] { new Claim(ClaimTypes.Role, role) };
    }

    private sealed class RecordingAuditService : IAuditService
    {
        public List<string> Actions { get; } = [];
        public Task LogAsync(string action, string module, string description, string? additionalData = null)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }
}
