/**
 * Sistema de Autenticação
 * Validação de e-mail corporativo @adventistas.org
 */

class Auth {
    constructor() {
        this.user = null;
        this.allowedDomain = '@adventistas.org';
        this.storageKey = 'consultas_user';
        this.init();
    }

    init() {
        // Verificar se já está logado
        const savedUser = localStorage.getItem(this.storageKey);
        if (savedUser) {
            try {
                this.user = JSON.parse(savedUser);
                this.showApp();
            } catch (e) {
                this.logout();
            }
        } else {
            this.showLogin();
        }

        // Event listeners
        document.getElementById('loginButton')?.addEventListener('click', () => this.showEmailPrompt());
        document.getElementById('logoutButton')?.addEventListener('click', () => this.logout());
    }

    showLogin() {
        document.getElementById('loginScreen').style.display = 'flex';
        document.getElementById('appScreen').style.display = 'none';
    }

    showApp() {
        document.getElementById('loginScreen').style.display = 'none';
        document.getElementById('appScreen').style.display = 'flex';

        if (this.user) {
            document.getElementById('userName').textContent = this.user.name;
            document.getElementById('userEmail').textContent = this.user.email;
        }

        // Iniciar aplicação
        if (window.app) {
            window.app.init();
        }
    }

    showEmailPrompt() {
        const email = prompt('Digite seu e-mail corporativo:');

        if (!email) {
            return;
        }

        if (!this.validateEmail(email)) {
            this.showError('E-mail inválido. Use um e-mail @adventistas.org');
            return;
        }

        const name = email.split('@')[0].split('.').map(
            word => word.charAt(0).toUpperCase() + word.slice(1)
        ).join(' ');

        this.user = {
            email: email,
            name: name,
            loginTime: new Date().toISOString()
        };

        localStorage.setItem(this.storageKey, JSON.stringify(this.user));
        this.hideError();
        this.showApp();
    }

    validateEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            return false;
        }
        return email.toLowerCase().endsWith(this.allowedDomain.toLowerCase());
    }

    logout() {
        if (confirm('Deseja realmente sair?')) {
            this.user = null;
            localStorage.removeItem(this.storageKey);
            this.showLogin();
        }
    }

    showError(message) {
        const errorDiv = document.getElementById('loginError');
        if (errorDiv) {
            errorDiv.textContent = message;
            errorDiv.style.display = 'block';
        }
    }

    hideError() {
        const errorDiv = document.getElementById('loginError');
        if (errorDiv) {
            errorDiv.style.display = 'none';
        }
    }

    getUser() {
        return this.user;
    }

    isAuthenticated() {
        return this.user !== null;
    }
}

// Instanciar autenticação
const auth = new Auth();
