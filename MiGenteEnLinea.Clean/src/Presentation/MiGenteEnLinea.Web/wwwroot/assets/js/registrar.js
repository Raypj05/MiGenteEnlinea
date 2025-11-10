function enviarCorreoActivacion(userID, email) {
    // Define los parámetros que se enviarán al WebMethod
    const parametros = {
        userID: userID, // Reemplazar con el ID real del usuario
        email: email // Reemplazar con el correo real
      
    };

    $.ajax({
        type: 'POST',
        url: '../../Registrar.aspx/EnviarCorreoActivacion',
        data: JSON.stringify(parametros), // Serializa los parámetros a JSON
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            Swal.fire(
                'Correo reenviado',
                'Se ha enviado nuevamente el correo de activación.',
                'success'
            ).then((result) => {
                if (result.isConfirmed) {
                    // Redirige a otra página
                    window.location.href = "Login.aspx"; // Cambia la URL según sea necesario
                }
            });
        },
        error: function (error) {
            Swal.fire(
                'Error',
                'Hubo un problema al reenviar el correo.',
                'error'
            );
        }
    });
}