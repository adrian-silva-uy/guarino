window.guarinoAuth = (() => {
    async function post(path, data) {
        try {
            const response = await fetch(path, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });
            const result = await response.json();
            if (!response.ok && !result.error) {
                result.error = response.status === 429
                    ? 'Demasiados intentos. Esperá unos minutos y volvé a intentar.'
                    : 'No se pudo completar la solicitud.';
            }
            return result;
        } catch {
            return { success: false, error: 'No se pudo conectar con el servidor.' };
        }
    }

    return {
        login: (userName, password) => post('/auth/login', { userName, password }),
        register: (userName, fullName, password) =>
            post('/auth/register', { userName, fullName, password }),
        logout: async () => {
            try {
                await fetch('/auth/logout', {
                    method: 'POST',
                    credentials: 'same-origin'
                });
            } catch {
                // La recarga del cliente fuerza la validación de la cookie vigente.
            }
        }
    };
})();
