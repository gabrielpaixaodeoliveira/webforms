# Sistema de Assinatura Digital de Documentos

Este projeto implementa um sistema de assinatura digital de documentos PDF utilizando o componente Web PKI da Lacuna Software em uma aplicação ASP.NET WebForms.

## Requisitos

- .NET Framework 4.5 ou superior
- Visual Studio 2019 ou superior
- Componente Web PKI da Lacuna Software
- Certificado digital válido instalado no navegador

## Estrutura do Projeto

```
├── Default.aspx              # Interface principal
├── Default.aspx.cs          # Lógica do servidor
├── Scripts/
│   ├── App/
│   │   └── signature-form.js # Biblioteca de assinatura
│   ├── lacuna-web-pki-2.11.0.js
│   ├── jquery-3.1.1.min.js
│   ├── jquery.blockUI.js
│   └── bootstrap.min.js
├── Uploads/                 # Diretório para arquivos enviados
├── Temp/                    # Diretório para arquivos temporários
└── Signed/                  # Diretório para arquivos assinados
```

## Configuração

1. Instale o componente Web PKI da Lacuna Software
2. Configure a licença do Web PKI em `LacunaPkiLicense.config`
3. Certifique-se que os diretórios `Uploads`, `Temp` e `Signed` existam e tenham permissões de escrita

## Fluxo de Execução

### Inicialização da Página

1. **Backend (Default.aspx.cs)**
```csharp
protected void Page_Load(object sender, EventArgs e)
{
    // Inicializa licença e diretórios
    InitializePkiLicense();
    EnsureDirectoriesExist();

    if (!IsPostBack)
    {
        InitializePage();
    }
    else
    {
        RestoreCertificateFromSession();
    }
}
```

2. **Frontend (Default.aspx)**
```javascript
Sys.Application.add_init(function() {
    // Inicializa o formulário de assinatura
    initializeSignatureForm();
    
    // Verifica se há hash para assinatura após postback
    if ($('#ToSignHashField').val()) {
        $('#CompleteSignButton').show();
    }
});
```

### Processo de Assinatura

#### 1. Seleção do Certificado e Upload do Arquivo

**Frontend:**
```javascript
function submitCertificateAndSign() {
    if (isSigning) return false;
    isSigning = true;
    
    // Armazena certificado selecionado
    var thumbprint = $('#certificateSelect').val();
    $('#SelectedCertThumbprintField').val(thumbprint);
    
    // Inicia processo de assinatura
    signatureForm.startSignature();
    return false;
}
```

**Backend:**
```csharp
protected void SubmitCertificateButton_Click(object sender, EventArgs e)
{
    try
    {
        // Valida certificado e arquivo
        ValidateCertificateSelection();
        HandleFileUpload();
        
        // Processa certificado e gera dados para assinatura
        ProcessCertificateAndGenerateSignature();
        
        // Atualiza UI
        UpdateUIForSignature();
    }
    catch (Exception ex)
    {
        HandleGeneralError(ex);
    }
}
```

#### 2. Geração da Assinatura

**Frontend:**
```javascript
function generateAndSubmitSignature() {
    if (isSigning) return false;
    isSigning = true;

    // Reinicializa o formulário
    initializeSignatureForm();
    
    setTimeout(function() {
        // Gera assinatura usando Web PKI
        pki.signHash({
            thumbprint: $('#SelectedCertThumbprintField').val(),
            hash: $('#ToSignHashField').val(),
            digestAlgorithm: $('#DigestAlgorithmField').val()
        }).success(function(signature) {
            // Envia assinatura para o servidor
            $('#SignatureField').val(signature);
            $('#SubmitSignatureButton').click();
        });
    }, 1000);
    
    return false;
}
```

**Backend:**
```csharp
protected void SubmitSignatureButton_Click(object sender, EventArgs e)
{
    try
    {
        // Valida assinatura
        ValidateSignatureSubmission();
        
        // Processa assinatura e salva arquivo
        ProcessSignatureAndSaveFile();
        
        // Limpa dados temporários e mostra sucesso
        CleanupAndShowSuccess();
    }
    catch (Exception ex)
    {
        HandleGeneralError(ex);
    }
}
```

### Gerenciamento de Estado

#### Session Storage
```csharp
private const string UploadedFileSessionKey = "UploadedFile";
private const string TransferDataSessionKey = "TransferData";
private const string SelectedCertificateSessionKey = "SelectedCertificate";
private const string SelectedCertThumbprintSessionKey = "SelectedCertThumbprint";
private const string SignedFilePathSessionKey = "SignedFilePath";
private const string FileNameSessionKey = "FileName";
```

#### Campos Ocultos
```html
<asp:HiddenField ID="CertificateField" runat="server" />
<asp:HiddenField ID="ToSignHashField" runat="server" />
<asp:HiddenField ID="DigestAlgorithmField" runat="server" />
<asp:HiddenField ID="SignatureField" runat="server" />
<asp:HiddenField ID="TransferDataFileIdField" runat="server" />
<asp:HiddenField ID="SelectedCertThumbprintField" runat="server" />
```

### Tratamento de Erros

#### Frontend
```javascript
// Tratamento de erros do Web PKI
pki.init({
    defaultError: function(message, error, origin) {
        console.error('Web PKI Error:', message, error, origin);
        alert('Error initializing digital signature component: ' + message);
        $.unblockUI();
        isSigning = false;
    }
});

// Tratamento de erros de postback
Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function (sender, args) {
    if (args.get_error()) {
        console.error('Postback error:', args.get_error());
        alert('An error has occurred on the server');
        $.unblockUI();
        isSigning = false;
    }
});
```

#### Backend
```csharp
private void HandleGeneralError(Exception ex)
{
    lblMessage.Text = "Error: " + ex.Message;
    lblMessage.CssClass = "message error";
    SignaturePanel.Update();
}
```

## Segurança

1. **Validação de Certificados**
   - Verifica validade do certificado
   - Confirma posse da chave privada
   - Valida cadeia de certificação

2. **Política de Assinatura**
   - Configuração PAdES básica
   - Validação de confiança
   - Representação visual da assinatura

3. **Gerenciamento de Arquivos**
   - Arquivos temporários são limpos após uso
   - Validação de tipos de arquivo (apenas PDF)
   - Nomes de arquivo seguros

## Tratamento de Erros

O sistema implementa tratamento de erros em vários níveis:

1. **Cliente**
   - Validação de certificado selecionado
   - Verificação de arquivo PDF
   - Erros de Web PKI
   - Tratamento de erros de postback

2. **Servidor**
   - Validação de certificado
   - Processamento de arquivo
   - Geração de assinatura
   - Limpeza de arquivos temporários

3. **Feedback ao Usuário**
   - Mensagens de erro claras
   - Logs detalhados no console
   - Indicadores visuais de progresso

## Dependências

- **Web PKI**: Componente de assinatura digital
- **jQuery**: Manipulação DOM e AJAX
- **jQuery BlockUI**: Bloqueio de UI durante operações
- **Bootstrap**: Interface do usuário
- **Microsoft Ajax**: Funcionalidades WebForms

## Desenvolvimento

Para contribuir com o projeto:

1. Clone o repositório
2. Instale as dependências
3. Configure a licença do Web PKI
4. Execute em modo de desenvolvimento

## Troubleshooting

Problemas comuns e soluções:

1. **Certificado não encontrado**
   - Verifique se o certificado está instalado
   - Confirme se o Web PKI está inicializado
   - Verifique os logs do console

2. **Erro na assinatura**
   - Valide o certificado
   - Verifique o arquivo PDF
   - Confirme as permissões de diretório

3. **Problemas de postback**
   - Verifique o estado do UpdatePanel
   - Confirme a inicialização do Web PKI
   - Valide os campos ocultos

## Licença

Este projeto utiliza a licença do Web PKI da Lacuna Software. Consulte a documentação oficial para mais detalhes. 