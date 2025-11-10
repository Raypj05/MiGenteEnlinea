using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiGenteEnLinea.Web.Services;
using System.Security.Claims;

namespace MiGenteEnLinea.Web.Controllers
{
    [Authorize]
    public class EmpleadorController : Controller
    {
        private readonly IApiService _apiService;
        private readonly ILogger<EmpleadorController> _logger;

        public EmpleadorController(IApiService apiService, ILogger<EmpleadorController> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        #region Dashboard

        /// <summary>
        /// GET: /Empleador/Index
        /// Main dashboard for Empleador
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tipo = User.FindFirst("tipo")?.Value;

                if (string.IsNullOrEmpty(userId) || tipo != "1")
                {
                    _logger.LogWarning("Usuario no autorizado intentó acceder al dashboard empleador");
                    return RedirectToAction("Login", "Auth");
                }

                // Get dashboard statistics
                var empleadosResponse = await _apiService.GetEmpleadosAsync(userId, activo: true);
                
                ViewBag.EmpleadosActivos = empleadosResponse.Success ? empleadosResponse.Data?.Count ?? 0 : 0;
                ViewBag.UserName = User.Identity?.Name ?? "Usuario";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading empleador dashboard for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                TempData["ErrorMessage"] = "Error al cargar el dashboard. Intente nuevamente.";
                return View();
            }
        }

        #endregion

        #region Calificaciones

        /// <summary>
        /// Página de Calificaciones - Consultar y calificar perfiles
        /// </summary>
        public IActionResult Calificaciones()
        {
            var tipo = User.FindFirst("tipo")?.Value;
            if (tipo != "1")
            {
                _logger.LogWarning("Usuario tipo {Tipo} intentó acceder a Calificaciones", tipo);
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        /// <summary>
        /// AJAX: Obtiene las calificaciones realizadas por el empleador actual
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMisCalificaciones()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var calificaciones = await _apiService.GetMisCalificacionesAsync(userId);
                return Json(new { success = true, data = calificaciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener calificaciones del usuario");
                return Json(new { success = false, message = "Error al cargar calificaciones" });
            }
        }

        /// <summary>
        /// AJAX: Obtiene lista de empleados inactivos para calificar
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPerfilesParaCalificar()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var perfiles = await _apiService.GetPerfilesParaCalificarAsync(userId);
                return Json(new { success = true, data = perfiles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener perfiles para calificar");
                return Json(new { success = false, message = "Error al cargar perfiles" });
            }
        }

        /// <summary>
        /// AJAX: Registra una calificación para un perfil
        /// Validación: No se puede calificar el mismo perfil más de una vez en 2 meses
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CalificarPerfil([FromBody] CalificarPerfilRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Validaciones
                if (string.IsNullOrEmpty(request.Identificacion) || string.IsNullOrEmpty(request.Nombre))
                {
                    return Json(new { success = false, message = "Identificación y nombre son requeridos" });
                }

                if (request.Puntualidad < 1 || request.Cumplimiento < 1 || 
                    request.Conocimientos < 1 || request.Recomendacion < 1)
                {
                    return Json(new { success = false, message = "Todas las calificaciones deben ser al menos 1 estrella" });
                }

                // Enviar a API
                var resultado = await _apiService.CalificarPerfilAsync(userId, request);

                if (resultado)
                {
                    return Json(new { success = true, message = "Calificación guardada exitosamente" });
                }
                else
                {
                    return Json(new { success = false, message = "No puedes calificar este perfil. Debe esperar al menos 2 meses desde tu última calificación." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calificar perfil");
                return Json(new { success = false, message = "Error al guardar calificación" });
            }
        }

        /// <summary>
        /// AJAX: Consulta un perfil por identificación y devuelve calificaciones promedio
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ConsultarPerfil([FromBody] ConsultarPerfilRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Identificacion))
                {
                    return Json(new { success = false, message = "Identificación es requerida" });
                }

                var perfil = await _apiService.ConsultarPerfilAsync(request.Identificacion);

                if (perfil == null)
                {
                    return Json(new { success = false, message = "No se encontró el perfil" });
                }

                return Json(new { success = true, data = perfil });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar perfil {Identificacion}", request.Identificacion);
                return Json(new { success = false, message = "Error al consultar perfil" });
            }
        }

        #endregion

        #region FichaColaboradorTemporal

        /// <summary>
        /// Página de Ficha de Colaborador Temporal
        /// </summary>
        public async Task<IActionResult> FichaColaboradorTemporal(int contratacionID)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Auth");
                }

                var ficha = await _apiService.GetFichaColaboradorTemporalAsync(contratacionID, userId);

                if (ficha == null)
                {
                    _logger.LogWarning("Ficha de colaborador temporal no encontrada: {ContratacionID}", contratacionID);
                    TempData["Error"] = "No se encontró el colaborador solicitado";
                    return RedirectToAction("Colaboradores");
                }

                ViewBag.ContratacionID = contratacionID;

                return View(ficha);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar ficha de colaborador temporal");
                TempData["Error"] = "Error al cargar información del colaborador";
                return RedirectToAction("Colaboradores");
            }
        }

        /// <summary>
        /// AJAX: Obtener trabajos de contratación por estatus
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTrabajosContratacion(int contratacionID, int estatus)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var trabajos = await _apiService.GetTrabajosContratacionAsync(contratacionID, estatus, userId);

                return Json(new { success = true, data = trabajos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener trabajos de contratación");
                return Json(new { success = false, message = "Error al cargar trabajos" });
            }
        }

        /// <summary>
        /// AJAX: Eliminar colaborador temporal
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarColaborador([FromBody] EliminarColaboradorRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.EliminarColaboradorTemporalAsync(request.ContratacionID, userId);

                if (resultado)
                {
                    return Json(new { success = true, message = "Colaborador eliminado correctamente" });
                }

                return Json(new { success = false, message = "No se pudo eliminar el colaborador" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar colaborador temporal");
                return Json(new { success = false, message = "Error al eliminar colaborador" });
            }
        }

        /// <summary>
        /// AJAX: Crear nueva contratación temporal
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> NuevaContratacion([FromBody] NuevaContratacionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.CrearContratacionTemporalAsync(userId, request);

                if (resultado)
                {
                    return Json(new { success = true, message = "Contratación creada correctamente" });
                }

                return Json(new { success = false, message = "No se pudo crear la contratación" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear nueva contratación");
                return Json(new { success = false, message = "Error al crear contratación" });
            }
        }

        #endregion

        #region AdquirirPlan

        /// <summary>
        /// GET: Mostrar planes disponibles para empleadores
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AdquirirPlan()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Auth");
                }

                // Obtener planes desde API
                var planes = await _apiService.GetPlanesEmpleadorAsync();

                return View(planes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar planes");
                TempData["Error"] = "Error al cargar los planes disponibles";
                return View(new List<Services.PlanDto>());
            }
        }

        /// <summary>
        /// POST: Procesar pago de suscripción con Cardnet
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcesarPago([FromBody] PagoSuscripcionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Validaciones básicas
                if (request.PlanID <= 0 || request.Monto <= 0)
                {
                    return Json(new { success = false, message = "Datos de plan inválidos" });
                }

                if (string.IsNullOrWhiteSpace(request.NombreTitular) ||
                    string.IsNullOrWhiteSpace(request.NumeroTarjeta) ||
                    string.IsNullOrWhiteSpace(request.FechaVencimiento) ||
                    string.IsNullOrWhiteSpace(request.CVV))
                {
                    return Json(new { success = false, message = "Complete todos los campos del formulario" });
                }

                // Procesar pago a través de API
                var resultado = await _apiService.ProcesarPagoSuscripcionAsync(userId, request);

                if (resultado)
                {
                    _logger.LogInformation("Pago procesado exitosamente para usuario {UserId}, plan {PlanID}", userId, request.PlanID);
                    return Json(new { success = true, message = "Suscripción completada correctamente" });
                }
                else
                {
                    _logger.LogWarning("Falló procesamiento de pago para usuario {UserId}", userId);
                    return Json(new { success = false, message = "Error al procesar el pago. Verifique los datos de su tarjeta." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar pago de suscripción");
                return Json(new { success = false, message = "Error al procesar el pago. Inténtelo nuevamente." });
            }
        }

        #endregion

        #region MiSuscripcion

        /// <summary>
        /// GET: Ver información de suscripción actual e historial de pagos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MiSuscripcion()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Auth");
                }

                // Obtener suscripción actual
                var suscripcion = await _apiService.GetMiSuscripcionAsync(userId);

                // Obtener historial de ventas
                var ventas = await _apiService.GetHistorialVentasAsync(userId);

                var viewModel = new MiSuscripcionViewModel
                {
                    Suscripcion = suscripcion,
                    Ventas = ventas
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar suscripción");
                TempData["Error"] = "Error al cargar información de suscripción";
                return View(new MiSuscripcionViewModel());
            }
        }

        /// <summary>
        /// POST: Cancelar suscripción actual
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CancelarSuscripcion()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.CancelarSuscripcionAsync(userId);

                if (resultado)
                {
                    _logger.LogInformation("Suscripción cancelada para usuario {UserId}", userId);
                    return Json(new { success = true, message = "Suscripción cancelada exitosamente" });
                }
                else
                {
                    _logger.LogWarning("Falló cancelación de suscripción para usuario {UserId}", userId);
                    return Json(new { success = false, message = "No se pudo cancelar la suscripción" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar suscripción");
                return Json(new { success = false, message = "Error al procesar la solicitud" });
            }
        }

        #endregion

        #region DetalleContratacion

        /// <summary>
        /// Página de Detalle de Contratación Temporal
        /// </summary>
        public async Task<IActionResult> DetalleContratacion(int contratacionID, int detalleID)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Auth");
                }

                var detalle = await _apiService.GetDetalleContratacionAsync(contratacionID, detalleID, userId);

                if (detalle == null)
                {
                    _logger.LogWarning("Detalle de contratación no encontrado: {ContratacionID}/{DetalleID}", contratacionID, detalleID);
                    return RedirectToAction("Colaboradores");
                }

                var viewModel = new DetalleContratacionViewModel
                {
                    ContratacionID = detalle.ContratacionID,
                    DetalleID = detalle.DetalleID,
                    NombreContratista = detalle.NombreContratista,
                    DescripcionCorta = detalle.DescripcionCorta,
                    DescripcionAmpliada = detalle.DescripcionAmpliada,
                    FechaInicio = detalle.FechaInicio,
                    FechaConclusion = detalle.FechaConclusion,
                    MontoAcordado = detalle.MontoAcordado,
                    EsquemaPagos = detalle.EsquemaPagos,
                    Estatus = detalle.Estatus,
                    PagosRealizados = detalle.PagosRealizados,
                    MontoPendiente = detalle.MontoPendiente,
                    Pagos = detalle.Pagos.Select(p => new PagoContratacionInfo
                    {
                        PagoID = p.PagoID,
                        FechaPago = p.FechaPago,
                        Monto = p.Monto,
                        ConceptoPago = p.ConceptoPago
                    }).ToList(),
                    Calificado = detalle.Calificado,
                    Puntualidad = detalle.Puntualidad,
                    Cumplimiento = detalle.Cumplimiento,
                    Conocimientos = detalle.Conocimientos,
                    Recomendacion = detalle.Recomendacion
                };

                ViewBag.ContratacionID = contratacionID;
                ViewBag.DetalleID = detalleID;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle de contratación");
                return RedirectToAction("Colaboradores");
            }
        }

        /// <summary>
        /// AJAX: Actualizar información de contratación
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ActualizarContratacion([FromBody] ActualizarContratacionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.ActualizarContratacionAsync(userId, request);

                if (resultado)
                {
                    return Json(new { success = true, message = "Contratación actualizada" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al actualizar contratación" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar contratación");
                return Json(new { success = false, message = "Error al actualizar contratación" });
            }
        }

        /// <summary>
        /// AJAX: Procesar pago de contratación temporal
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcesarPagoContratacion([FromBody] ProcesarPagoContratacionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Validación: el total no puede exceder el monto pendiente
                if (request.Detalles == null || !request.Detalles.Any())
                {
                    return Json(new { success = false, message = "Debe agregar al menos un concepto de pago" });
                }

                var resultado = await _apiService.ProcesarPagoContratacionAsync(userId, request);

                if (resultado != null && resultado.PagoID > 0)
                {
                    return Json(new { success = true, data = new { pagoID = resultado.PagoID } });
                }
                else
                {
                    return Json(new { success = false, message = "Error al procesar pago" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar pago de contratación");
                return Json(new { success = false, message = "Error al procesar pago" });
            }
        }

        /// <summary>
        /// AJAX: Anular recibo de contratación
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AnularReciboContratacion([FromBody] AnularReciboRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.AnularReciboContratacionAsync(request.PagoID);

                if (resultado)
                {
                    return Json(new { success = true, message = "Recibo anulado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al anular recibo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al anular recibo de contratación");
                return Json(new { success = false, message = "Error al anular recibo" });
            }
        }

        /// <summary>
        /// AJAX: Cancelar trabajo sin concluir
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CancelarTrabajo([FromBody] CancelarTrabajoRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.CancelarTrabajoAsync(request.ContratacionID, request.DetalleID);

                if (resultado)
                {
                    return Json(new { success = true, message = "Trabajo cancelado" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al cancelar trabajo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar trabajo");
                return Json(new { success = false, message = "Error al cancelar trabajo" });
            }
        }

        /// <summary>
        /// AJAX: Finalizar trabajo
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> FinalizarTrabajo([FromBody] FinalizarTrabajoRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var resultado = await _apiService.FinalizarTrabajoAsync(request.ContratacionID, request.DetalleID);

                if (resultado)
                {
                    return Json(new { success = true, message = "Trabajo finalizado" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al finalizar trabajo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al finalizar trabajo");
                return Json(new { success = false, message = "Error al finalizar trabajo" });
            }
        }

        /// <summary>
        /// AJAX: Calificar contratación
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CalificarContratacion([FromBody] CalificarContratacionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Validación
                if (request.Puntualidad < 1 || request.Cumplimiento < 1 || 
                    request.Conocimientos < 1 || request.Recomendacion < 1)
                {
                    return Json(new { success = false, message = "Todas las calificaciones deben ser al menos 1 estrella" });
                }

                var resultado = await _apiService.CalificarContratacionAsync(userId, request);

                if (resultado)
                {
                    return Json(new { success = true, message = "Calificación guardada" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al calificar" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calificar contratación");
                return Json(new { success = false, message = "Error al calificar" });
            }
        }

        /// <summary>
        /// Imprimir recibo de contratación (abre ventana nueva)
        /// </summary>
        public IActionResult ImprimirReciboContratacion(int pagoID)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // TODO: Implementar generación de PDF
            // Por ahora retorna URL placeholder
            return Redirect($"/Empleador/Impresion/ReciboContratacion?pagoID={pagoID}&userId={userId}");
        }

        #endregion

        /// <summary>
        /// Gestión de Colaboradores - Página principal del empleador
        /// Muestra 4 tabs: Empleados Fijos (Activos/Historial), Contrataciones Temporales (Activos/Historial)
        /// </summary>
        public IActionResult Colaboradores()
        {
            // Verificar que el usuario tenga el rol correcto
            var tipo = User.FindFirst("tipo")?.Value;
            if (tipo != "1") // Solo Empleadores (tipo=1)
            {
                _logger.LogWarning("Usuario tipo {Tipo} intentó acceder a Colaboradores (solo Empleadores)", tipo);
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        #region AJAX Endpoints para carga dinámica de tablas

        /// <summary>
        /// WebMethod: GetColaboradores - Obtiene empleados fijos activos con paginación y búsqueda
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetColaboradores([FromBody] ColaboradoresRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Llamar al API para obtener empleados
                var response = await _apiService.GetEmpleadosAsync(userId, activo: true);
                
                if (!response.Success || response.Data == null)
                {
                    return Json(new { 
                        colaboradores = new List<object>(), 
                        totalRecords = 0 
                    });
                }

                var empleados = response.Data;

                // Filtrar por término de búsqueda
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    empleados = empleados.Where(e => 
                        (e.Nombre + " " + e.Apellido).Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (e.Identificacion ?? "").Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                var totalRecords = empleados.Count;

                // Paginación
                var paginatedEmpleados = empleados
                    .OrderByDescending(e => e.FechaRegistro)
                    .Skip((request.PageIndex - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(e => new
                    {
                        empleadoID = e.EmpleadoID,
                        fechaInicio = e.FechaInicio?.ToString("o") ?? "",
                        identificacion = e.Identificacion ?? "",
                        Nombre = $"{e.Nombre} {e.Apellido}",
                        salario = e.Salario?.ToString("N2") ?? "0.00",
                        diasPago = e.DiasPago ?? 0,
                        foto = !string.IsNullOrEmpty(e.Foto) ? e.Foto : "/img/default-avatar.png"
                    })
                    .ToList();

                return Json(new
                {
                    colaboradores = paginatedEmpleados,
                    totalRecords = totalRecords
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener colaboradores activos");
                return Json(new { 
                    colaboradores = new List<object>(), 
                    totalRecords = 0,
                    error = "Error al cargar los datos"
                });
            }
        }

        /// <summary>
        /// WebMethod: GetColaboradoresInactivos - Obtiene empleados fijos inactivos (historial)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetColaboradoresInactivos([FromBody] ColaboradoresRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Llamar al API para obtener empleados inactivos
                var response = await _apiService.GetEmpleadosAsync(userId, activo: false);
                
                if (!response.Success || response.Data == null)
                {
                    return Json(new { 
                        colaboradoresInactivos = new List<object>(), 
                        totalRecords = 0 
                    });
                }

                var empleados = response.Data;

                // Filtrar por término de búsqueda
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    empleados = empleados.Where(e => 
                        (e.Nombre + " " + e.Apellido).Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (e.Identificacion ?? "").Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                var totalRecords = empleados.Count;

                // Paginación
                var paginatedEmpleados = empleados
                    .OrderByDescending(e => e.FechaRegistro)
                    .Skip((request.PageIndex - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(e => new
                    {
                        empleadoID = e.EmpleadoID,
                        fechaInicio = e.FechaInicio?.ToString("dd-MMM-yyyy") ?? "",
                        identificacion = e.Identificacion ?? "",
                        Nombre = $"{e.Nombre} {e.Apellido}",
                        salario = e.Salario?.ToString("N2") ?? "0.00",
                        diasPago = e.DiasPago ?? 0,
                        foto = !string.IsNullOrEmpty(e.Foto) ? e.Foto : "/img/default-avatar.png",
                        fechaSalida = e.FechaSalida?.ToString("dd-MMM-yyyy") ?? "N/A"
                    })
                    .ToList();

                return Json(new
                {
                    colaboradoresInactivos = paginatedEmpleados,
                    totalRecords = totalRecords
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener colaboradores inactivos");
                return Json(new { 
                    colaboradoresInactivos = new List<object>(), 
                    totalRecords = 0,
                    error = "Error al cargar los datos"
                });
            }
        }

        /// <summary>
        /// WebMethod: GetContratacionesTemporales - Obtiene contrataciones temporales (activas o historial según estatus)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetContratacionesTemporales([FromBody] ContratacionesRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Llamar al API para obtener contrataciones temporales
                var response = await _apiService.GetContratacionesTemporalesAsync(userId, request.Estatus);
                
                if (!response.Success || response.Data == null)
                {
                    return Json(new { 
                        contratacionesTemporales = new List<object>(), 
                        totalRecords = 0 
                    });
                }

                var contrataciones = response.Data;

                // Filtrar por término de búsqueda
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    contrataciones = contrataciones.Where(c => 
                        (c.Nombre ?? "").Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (c.Identificacion ?? "").Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (c.NombreComercial ?? "").Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                var totalRecords = contrataciones.Count;

                // Paginación
                var paginatedContrataciones = contrataciones
                    .OrderByDescending(c => c.FechaRegistro)
                    .Skip((request.PageIndex - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(c => new
                    {
                        contratacionID = c.ContratacionID,
                        fechaRegistro = c.FechaRegistro?.ToString("dd-MMM-yyyy") ?? "",
                        identificacion = $"{c.Identificacion}{c.RNC}",
                        Nombre = $"{c.NombreComercial}{c.Nombre} {c.Apellido}",
                        telefono1 = c.Telefono1 ?? "",
                        telefono2 = c.Telefono2 ?? "",
                        foto = !string.IsNullOrEmpty(c.Foto) ? c.Foto : "/img/default-avatar.png"
                    })
                    .ToList();

                return Json(new
                {
                    contratacionesTemporales = paginatedContrataciones,
                    totalRecords = totalRecords
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener contrataciones temporales");
                return Json(new { 
                    contratacionesTemporales = new List<object>(), 
                    totalRecords = 0,
                    error = "Error al cargar los datos"
                });
            }
        }

        #endregion

        #region Ficha de Empleado

        /// <summary>
        /// Ficha detallada de empleado - Visualización y gestión
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> FichaEmpleado(int? empleadoID)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Auth");
            }

            if (empleadoID == null)
            {
                // Nuevo empleado - Redirigir a formulario de creación (implementar después)
                return RedirectToAction("Colaboradores");
            }

            // Obtener datos del empleado desde la API
            var response = await _apiService.GetEmpleadoByIdAsync(userId, empleadoID.Value);
            
            if (!response.Success || response.Data == null)
            {
                TempData["Error"] = "Empleado no encontrado";
                return RedirectToAction("Colaboradores");
            }

            // Obtener historial de pagos
            var pagosResponse = await _apiService.GetPagosEmpleadoAsync(empleadoID.Value);
            
            var viewModel = new FichaEmpleadoViewModel
            {
                Empleado = response.Data,
                Pagos = pagosResponse.Success && pagosResponse.Data != null 
                    ? pagosResponse.Data 
                    : new List<PagoEmpleadoDto>()
            };

            return View(viewModel);
        }

        /// <summary>
        /// WebMethod: EliminarRecibo - Eliminar un recibo de pago (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarRecibo([FromBody] EliminarReciboRequest request)
        {
            try
            {
                var response = await _apiService.EliminarReciboAsync(request.PagoID);
                
                if (response.Success)
                {
                    return Json(new { success = true, message = "Recibo eliminado correctamente" });
                }
                else
                {
                    return Json(new { success = false, message = response.Message ?? "Error al eliminar recibo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar recibo {PagoID}", request.PagoID);
                return Json(new { success = false, message = "Error al eliminar recibo" });
            }
        }

        /// <summary>
        /// WebMethod: ProcesarPago - Procesar pago de nómina (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcesarPago([FromBody] ProcesarPagoRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Validar request
                if (request.EmpleadoID <= 0 || request.Detalle == null || !request.Detalle.Any())
                {
                    return Json(new { success = false, message = "Datos de pago inválidos" });
                }

                // Agregar userId al request
                request.UserID = userId;

                // Llamar API para procesar pago
                var response = await _apiService.ProcesarPagoAsync(request);

                if (response.Success && response.Data != null)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Pago procesado exitosamente",
                        reciboID = response.Data.ReciboID,
                        urlPdf = response.Data.UrlPdf
                    });
                }

                return Json(new { success = false, message = response.Message ?? "Error al procesar el pago" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar pago de nómina para empleado {EmpleadoID}", request.EmpleadoID);
                return Json(new { success = false, message = "Error al procesar el pago" });
            }
        }

        /// <summary>
        /// Acción: MiPerfil - Página de gestión de perfil del empleador
        /// </summary>
        public async Task<IActionResult> MiPerfil()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Auth");
                }

                // Obtener datos del perfil
                var perfilResponse = await _apiService.GetPerfilEmpleadorAsync(userId);

                var viewModel = new PerfilEmpleadorViewModel
                {
                    Perfil = perfilResponse.Success ? perfilResponse.Data : null,
                    MaxUsuarios = 2 // Por defecto, puede venir del plan
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar perfil del empleador");
                TempData["Error"] = "Error al cargar el perfil";
                return RedirectToAction("Colaboradores");
            }
        }

        /// <summary>
        /// WebMethod: ActualizarPerfil - Actualizar datos del perfil (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                request.UserID = userId;

                var response = await _apiService.ActualizarPerfilAsync(request);

                if (response.Success)
                {
                    return Json(new { success = true, message = "Perfil actualizado correctamente" });
                }

                return Json(new { success = false, message = response.Message ?? "Error al actualizar perfil" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar perfil");
                return Json(new { success = false, message = "Error al actualizar el perfil" });
            }
        }

        /// <summary>
        /// WebMethod: GetCredenciales - Obtener lista de credenciales (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetCredenciales()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var response = await _apiService.GetCredencialesAsync(userId);

                if (response.Success)
                {
                    return Json(new { success = true, data = response.Data });
                }

                return Json(new { success = false, message = response.Message ?? "Error al obtener credenciales" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener credenciales");
                return Json(new { success = false, message = "Error al cargar credenciales" });
            }
        }

        /// <summary>
        /// WebMethod: CrearCredencial - Crear nueva credencial de acceso (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CrearCredencial([FromBody] CrearCredencialRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                request.UserID = userId;

                var response = await _apiService.CrearCredencialAsync(request);

                if (response.Success)
                {
                    return Json(new { success = true, message = "Credencial creada correctamente" });
                }

                return Json(new { success = false, message = response.Message ?? "Error al crear credencial" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear credencial");
                return Json(new { success = false, message = "Error al crear la credencial" });
            }
        }

        /// <summary>
        /// WebMethod: ResetPassword - Enviar email de recuperación de contraseña (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var response = await _apiService.ResetPasswordAsync(request.Email);

                if (response.Success)
                {
                    return Json(new { success = true, message = "Email de recuperación enviado" });
                }

                return Json(new { success = false, message = response.Message ?? "Error al enviar email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email de recuperación");
                return Json(new { success = false, message = "Error al enviar el email" });
            }
        }

        #endregion

        #region Request Models para AJAX

        public class ColaboradoresRequest
        {
            public int PageIndex { get; set; } = 1;
            public int PageSize { get; set; } = 10;
            public string? SearchTerm { get; set; }
        }

        public class ContratacionesRequest
        {
            public int PageIndex { get; set; } = 1;
            public int PageSize { get; set; } = 10;
            public string? SearchTerm { get; set; }
            public int Estatus { get; set; } // 1 = Activo, 2 = Inactivo/Historial
        }

        public class EliminarReciboRequest
        {
            public int PagoID { get; set; }
        }

        public class ProcesarPagoRequest
        {
            public string UserID { get; set; } = string.Empty;
            public int EmpleadoID { get; set; }
            public string Concepto { get; set; } = string.Empty; // "Salario" o "Regalia"
            public DateTime FechaPago { get; set; }
            public int Periodo { get; set; } // 1 = Completo, 2 = Fracción
            public List<DetallePagoItem> Detalle { get; set; } = new();
        }

        public class DetallePagoItem
        {
            public string Concepto { get; set; } = string.Empty;
            public decimal Monto { get; set; }
        }

        public class ActualizarPerfilRequest
        {
            public string UserID { get; set; } = string.Empty;
            public int TipoIdentificacion { get; set; }
            public string Identificacion { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string Apellido { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Telefono1 { get; set; }
            public string? Telefono2 { get; set; }
            public string Direccion { get; set; } = string.Empty;
            public string? NombreComercial { get; set; }
            public string? NombreGerente { get; set; }
            public string? ApellidoGerente { get; set; }
            public string? DireccionGerente { get; set; }
        }

        public class CrearCredencialRequest
        {
            public string UserID { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool Activo { get; set; }
        }

        public class ResetPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        public class CalificarPerfilRequest
        {
            public int Tipo { get; set; }
            public string Identificacion { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public int Puntualidad { get; set; }
            public int Cumplimiento { get; set; }
            public int Conocimientos { get; set; }
            public int Recomendacion { get; set; }
        }

        public class ConsultarPerfilRequest
        {
            public string Identificacion { get; set; } = string.Empty;
        }

        public class ActualizarContratacionRequest
        {
            public int ContratacionID { get; set; }
            public int DetalleID { get; set; }
            public string DescripcionCorta { get; set; } = string.Empty;
            public string DescripcionAmpliada { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime FechaConclusion { get; set; }
            public decimal MontoAcordado { get; set; }
            public string EsquemaPagos { get; set; } = string.Empty;
        }

        public class ProcesarPagoContratacionRequest
        {
            public int ContratacionID { get; set; }
            public int DetalleID { get; set; }
            public DateTime FechaPago { get; set; }
            public string ConceptoPago { get; set; } = string.Empty;
            public List<DetalleConceptoPago> Detalles { get; set; } = new();
        }

        public class DetalleConceptoPago
        {
            public string Concepto { get; set; } = string.Empty;
            public decimal Monto { get; set; }
        }

        public class AnularReciboRequest
        {
            public int PagoID { get; set; }
        }

        public class CancelarTrabajoRequest
        {
            public int ContratacionID { get; set; }
            public int DetalleID { get; set; }
        }

        public class FinalizarTrabajoRequest
        {
            public int ContratacionID { get; set; }
            public int DetalleID { get; set; }
        }

        public class CalificarContratacionRequest
        {
            public int ContratacionID { get; set; }
            public int DetalleID { get; set; }
            public int Puntualidad { get; set; }
            public int Cumplimiento { get; set; }
            public int Conocimientos { get; set; }
            public int Recomendacion { get; set; }
        }

        public class EliminarColaboradorRequest
        {
            public int ContratacionID { get; set; }
        }

        public class NuevaContratacionRequest
        {
            public int ContratacionID { get; set; }
            public string DescripcionCorta { get; set; } = string.Empty;
            public string DescripcionAmpliada { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime FechaConclusion { get; set; }
            public decimal MontoAcordado { get; set; }
            public string EsquemaPagos { get; set; } = string.Empty;
        }

        public class PagoSuscripcionRequest
        {
            public int PlanID { get; set; }
            public string NombreTitular { get; set; } = string.Empty;
            public string NumeroTarjeta { get; set; } = string.Empty;
            public string FechaVencimiento { get; set; } = string.Empty;
            public string CVV { get; set; } = string.Empty;
            public decimal Monto { get; set; }
        }

        #endregion

        #region ViewModels

        public class FichaEmpleadoViewModel
        {
            public EmpleadoDto Empleado { get; set; } = new();
            public List<PagoEmpleadoDto> Pagos { get; set; } = new();
        }

        public class PerfilEmpleadorViewModel
        {
            public PerfilEmpleadorDto? Perfil { get; set; }
            public int MaxUsuarios { get; set; }
        }

        public class FichaColaboradorTemporalViewModel
        {
            public int ContratacionID { get; set; }
            public DateTime FechaRegistro { get; set; }
            public int Tipo { get; set; } // 1 = Persona Física, 2 = Empresa
            public string Identificacion { get; set; } = string.Empty;
            public string RNC { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string Apellido { get; set; } = string.Empty;
            public string NombreComercial { get; set; } = string.Empty;
            public string NombreCompleto { get; set; } = string.Empty;
            public string Direccion { get; set; } = string.Empty;
            public string Provincia { get; set; } = string.Empty;
            public string Municipio { get; set; } = string.Empty;
            public string Telefono1 { get; set; } = string.Empty;
            public string? Telefono2 { get; set; }
            public string? Foto { get; set; }
            public string? NombreRepresentante { get; set; }
            public string? CedulaRepresentante { get; set; }
        }

        public class DetalleContratacionViewModel
        {
            public int ContratacionID { get; set; }
            public int DetalleID { get; set; }
            public string NombreContratista { get; set; } = string.Empty;
            public string DescripcionCorta { get; set; } = string.Empty;
            public string DescripcionAmpliada { get; set; } = string.Empty;
            public DateTime? FechaInicio { get; set; }
            public DateTime? FechaConclusion { get; set; }
            public decimal MontoAcordado { get; set; }
            public string EsquemaPagos { get; set; } = string.Empty;
            public int Estatus { get; set; } // 1=Activo, 2=Finalizado, 3=Cancelado
            public decimal PagosRealizados { get; set; }
            public decimal MontoPendiente { get; set; }
            public List<PagoContratacionInfo> Pagos { get; set; } = new();
            public bool Calificado { get; set; }
            public int Puntualidad { get; set; }
            public int Cumplimiento { get; set; }
            public int Conocimientos { get; set; }
            public int Recomendacion { get; set; }
        }

        public class MiSuscripcionViewModel
        {
            public SuscripcionInfo? Suscripcion { get; set; }
            public List<VentaInfo> Ventas { get; set; } = new();
        }

        public class SuscripcionInfo
        {
            public string NombrePlan { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime ProximoPago { get; set; }
            public bool EstaActiva { get; set; }
        }

        public class VentaInfo
        {
            public int VentaID { get; set; }
            public DateTime Fecha { get; set; }
            public string IdTransaccion { get; set; } = string.Empty;
            public string NombrePlan { get; set; } = string.Empty;
            public decimal Precio { get; set; }
            public string TarjetaEnmascarada { get; set; } = string.Empty;
        }

        public class PagoContratacionInfo
        {
            public int PagoID { get; set; }
            public DateTime FechaPago { get; set; }
            public decimal Monto { get; set; }
            public string ConceptoPago { get; set; } = string.Empty;
        }

        #endregion
    }
}
