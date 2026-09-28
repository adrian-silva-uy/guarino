using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Domain.Entities;
using MediatR;

namespace GuarinoApp.Application.Modules.Stock.Commands.CrearPallet;
public class CrearPalletCommandHandler : IRequestHandler<CrearPalletCommand, int>
    {
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public CrearPalletCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
        => (_context, _currentUser) = (context, currentUser);

    public async Task<int> Handle(CrearPalletCommand request, CancellationToken ct)
        {
        var entity = new Pallet
            {
            VolcadoId = request.VolcadoId,
            Codigo = request.Codigo,
            Calibre = request.Calibre,
            Envase = request.Envase,
            Marca = request.Marca,
            Cliente = request.Cliente,
            Estado = request.Estado,
            CreatedBy = _currentUser.UserName
            };

        _context.Pallets.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
        }
    }
