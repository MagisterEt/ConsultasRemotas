# ✅ Checklist de Deployment - Ubuntu

Use este checklist para garantir que todos os passos foram executados corretamente.

---

## 📋 Antes de Começar

- [ ] Servidor Ubuntu 20.04+ ou 22.04 LTS instalado
- [ ] Sistema atualizado (`sudo apt update && sudo apt upgrade`)
- [ ] Acesso root/sudo disponível
- [ ] Conexão com internet ativa
- [ ] Credenciais SQL Server em mãos
  - [ ] Usuário SQL
  - [ ] Senha SQL
- [ ] (Opcional) Credenciais Azure AD para SharePoint
  - [ ] Tenant ID
  - [ ] Client ID
  - [ ] Client Secret
  - [ ] Site ID
  - [ ] Drive ID

---

## 🔧 Configuração Inicial

- [ ] Repositório clonado
  ```bash
  git clone https://github.com/MagisterEt/ConsultasRemotas.git
  cd ConsultasRemotas/Backend
  ```

- [ ] Wizard de configuração executado
  ```bash
  ./setup-wizard.sh
  ```

- [ ] Arquivo `.env` criado e verificado
  ```bash
  cat .env | grep SQL_USER
  ```

- [ ] Permissões do `.env` corretas (600)
  ```bash
  ls -la .env
  ```

---

## ✔️ Validação de Pré-requisitos

- [ ] Script de validação executado
  ```bash
  ./validate-prereqs.sh
  ```

- [ ] Todos os testes passaram (ou apenas warnings)

- [ ] Conectividade SQL testada
  ```bash
  ./test-sql-connection.sh
  ```

- [ ] Porta 8080 disponível

- [ ] Recursos suficientes:
  - [ ] Mínimo 2GB de espaço em disco
  - [ ] Mínimo 1GB de RAM disponível

---

## 🚀 Deployment

### Escolha o Método de Deployment

**Opção A: Docker** _(Recomendado)_

- [ ] Script de deploy executado
  ```bash
  ./deploy-docker.sh
  ```

- [ ] Docker instalado corretamente
  ```bash
  docker --version
  docker compose version
  ```

- [ ] Containers iniciados com sucesso
  ```bash
  docker compose ps
  ```

- [ ] Nenhum erro nos logs
  ```bash
  docker compose logs | grep -i error
  ```

**OU**

**Opção B: Instalação Direta**

- [ ] Script de deploy executado
  ```bash
  sudo ./deploy-ubuntu.sh
  ```

- [ ] .NET 8 instalado
  ```bash
  dotnet --version
  ```

- [ ] ODBC Driver instalado
  ```bash
  odbcinst -j
  ```

- [ ] Serviço systemd ativo
  ```bash
  sudo systemctl status consultas-remotas
  ```

- [ ] Nenhum erro nos logs
  ```bash
  sudo journalctl -u consultas-remotas -n 50
  ```

---

## ✅ Verificação de Funcionamento

- [ ] Health check respondendo
  ```bash
  curl http://localhost:8080/health
  # Deve retornar: {"status":"Healthy"}
  ```

- [ ] API status respondendo
  ```bash
  curl http://localhost:8080/api/status
  # Deve retornar informações do sistema
  ```

- [ ] Swagger UI acessível
  ```
  http://localhost:8080/swagger
  ```

- [ ] Teste de consulta SQL bem-sucedido
  ```bash
  curl -X POST http://localhost:8080/api/consultar \
    -H "Content-Type: application/json" \
    -d '{"query":"SELECT 1 as Teste","servidor":"Server1","banco":"AASI"}'
  ```

- [ ] SignalR Hub respondendo (opcional)
  ```
  ws://localhost:8080/hubs/logs
  ```

---

## 🎨 Frontend (Opcional)

- [ ] Diretório wwwroot criado
  ```bash
  mkdir -p ConsultasRemotas.Api/wwwroot
  ```

- [ ] Arquivos HTML/CSS/JS copiados
  ```bash
  cp -r ../../templates/* ConsultasRemotas.Api/wwwroot/
  cp -r ../../static/* ConsultasRemotas.Api/wwwroot/
  ```

- [ ] URLs da API atualizadas no JavaScript
  - [ ] Alterado de `http://localhost:5555` para `http://localhost:8080`

- [ ] SignalR configurado no frontend
  - [ ] Script SignalR incluído
  - [ ] Conexão ao hub implementada

- [ ] Aplicação reiniciada
  ```bash
  docker compose restart  # Docker
  # OU
  sudo systemctl restart consultas-remotas  # Instalação direta
  ```

- [ ] Frontend acessível via navegador
  ```
  http://localhost:8080/
  ```

---

## 🔒 Segurança e Firewall

- [ ] Firewall configurado (se necessário)
  ```bash
  sudo ufw allow 8080/tcp
  sudo ufw status
  ```

- [ ] Porta SQL Server protegida (não exposta publicamente)
  ```bash
  sudo ufw status | grep 1433
  ```

- [ ] Arquivo `.env` com permissões restritivas
  ```bash
  chmod 600 .env
  ```

- [ ] Credenciais documentadas em local seguro
  - [ ] Backup do `.env` em local seguro
  - [ ] Documentação de como recuperar credenciais

---

## 💾 Backup e Manutenção

- [ ] Backup inicial criado
  ```bash
  tar -czf backup-$(date +%Y%m%d).tar.gz \
    ConsultasRemotas.Api/ .env docker-compose.yml
  ```

- [ ] Logs sendo rotacionados
  - [ ] Docker: Limite de tamanho configurado
  - [ ] Systemd: Journal rotation ativa

- [ ] Monitoramento configurado (opcional)
  - [ ] Health checks periódicos
  - [ ] Alertas de erro

---

## 🧪 Testes Funcionais

- [ ] Consulta em servidor único funciona
- [ ] Consulta multi-servidor funciona
- [ ] Exportação CSV funciona
- [ ] Exportação Excel funciona
- [ ] Exportação Parquet funciona
- [ ] Upload SharePoint funciona (se configurado)
- [ ] Cancelamento de consulta funciona
- [ ] Logs em tempo real funcionam (SignalR)

---

## 🔄 Teste de Reinicialização

- [ ] Sistema sobrevive a reinicialização do servidor
  ```bash
  sudo reboot
  # Após reiniciar:
  curl http://localhost:8080/health
  ```

- [ ] Serviço inicia automaticamente no boot
  - [ ] Docker: `docker compose ps` mostra containers ativos
  - [ ] Systemd: `systemctl status consultas-remotas` mostra "enabled"

---

## 📊 Performance

- [ ] Tempo de resposta aceitável (< 5s para consultas simples)
- [ ] Uso de memória dentro do esperado (< 500MB em idle)
- [ ] Uso de CPU dentro do esperado (< 10% em idle)
- [ ] Logs não mostram memory leaks ou deadlocks

---

## 📚 Documentação

- [ ] Credenciais documentadas
- [ ] IPs dos servidores SQL documentados
- [ ] Procedimentos de backup documentados
- [ ] Procedimentos de restore documentados
- [ ] Contatos de suporte documentados

---

## 🆘 Troubleshooting Verificado

- [ ] Sabe como ver logs
  ```bash
  docker compose logs -f  # Docker
  sudo journalctl -u consultas-remotas -f  # Systemd
  ```

- [ ] Sabe como reiniciar serviço
  ```bash
  docker compose restart  # Docker
  sudo systemctl restart consultas-remotas  # Systemd
  ```

- [ ] Sabe como testar conexão SQL
  ```bash
  ./test-sql-connection.sh
  ```

- [ ] Sabe onde encontrar documentação
  - [ ] README.md
  - [ ] QUICKSTART-UBUNTU.md
  - [ ] Este checklist

---

## ✨ Deployment Concluído!

**Data do deployment:** ___/___/______

**Versão deployada:** _____________

**Responsável:** _____________

**Observações:**
```
_______________________________________
_______________________________________
_______________________________________
```

---

## 📞 Contatos de Suporte

| Área | Contato |
|------|---------|
| Infraestrutura | __________ |
| Banco de Dados | __________ |
| Desenvolvimento | __________ |
| Azure/SharePoint | __________ |

---

**✅ Todos os itens verificados? Parabéns! Seu sistema está em produção!** 🎉
