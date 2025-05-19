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
│   └── bootstrap.min.js
├── Uploads/                 # Diretório para arquivos enviados
├── Temp/                    # Diretório para arquivos temporários
└── Signed/                  # Diretório para arquivos assinados
```

## Configuração

1. Instale o componente Web PKI da Lacuna Software
2. Configure a licença do Web PKI em `LacunaPkiLicense.config`
3. Certifique-se que os diretórios `Uploads`, `Temp` e `Signed` existam e tenham permissões de escrita

## Fluxo de Execução Detalhado

### Inicialização da Página

1. **Backend (Default.aspx.cs)**
```csharp
protected void Page_Load(object sender, EventArgs e)
{
    // Carrega a licença do Web PKI
    PkiConfig.LoadLicense($"{Server.MapPath(".")}\\LacunaPkiLicense.config");

    if (!IsPostBack)
    {
        // Inicialização inicial da página
        lblMessage.Text = "";
        // Cria diretórios necessários
        CreateDirectories();
    }
    else
    {
        // Restaura certificado da sessão após postback
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

#### 1. Seleção do Certificado

**Frontend:**
```javascript
// Armazena o certificado selecionado
$('#certificateSelect').on('change', function() {
    var thumbprint = $(this).val();
    $('#SelectedCertThumbprintField').val(thumbprint);
});

// Inicializa o Web PKI
function initializeSignatureForm() {
    // Configura o formulário
    signatureForm.pageLoad({
        certificateSelect: $('#certificateSelect'),
        submitCertificateButton: $('#SubmitCertificateButton'),
        // ... outros campos
    });

    // Inicializa o Web PKI se necessário
    if (!pki) {
        pki = new LacunaWebPKI();
        pki.init({
            ready: function() {
                // Web PKI pronto para uso
            }
        });
    }
}
```

#### 2. Início da Assinatura

**Frontend:**
```javascript
function submitCertificateAndSign() {
    // Armazena certificado selecionado
    var thumbprint = $('#certificateSelect').val();
    $('#SelectedCertThumbprintField').val(thumbprint);
    
    // Inicia processo de assinatura
    signatureForm.startSignature();
}
```

**Backend:**
```csharp
protected void SubmitCertificateButton_Click(object sender, EventArgs e)
{
    try
    {
        // Valida certificado
        var cert = PKCertificate.Decode(Convert.FromBase64String(CertificateField.Value));
        
        // Processa arquivo PDF
        if (FileUpload1.HasFile)
        {
            // Salva arquivo temporário
            string tempPath = SaveUploadedFile();
            Session[UploadedFileSessionKey] = tempPath;
        }

        // Prepara documento para assinatura
        var padesSigner = new PadesSigner();
        padesSigner.SetPdfToSign(File.ReadAllBytes(filePath));
        padesSigner.SetSigningCertificate(cert);
        
        // Gera hash para assinatura
        byte[] toSignBytes = padesSigner.GetToSignBytes(out SignatureAlgorithm signatureAlg, out byte[] transferData);
        
        // Armazena dados na sessão
        Session[TransferDataSessionKey] = transferData;
        
        // Configura campos para assinatura
        ToSignHashField.Value = Convert.ToBase64String(signatureAlg.DigestAlgorithm.ComputeHash(toSignBytes));
        DigestAlgorithmField.Value = signatureAlg.DigestAlgorithm.Oid;
    }
    catch (Exception ex)
    {
        // Tratamento de erro
    }
}
```

#### 3. Geração da Assinatura

**Frontend:**
```javascript
function generateAndSubmitSignature() {
    // Reinitializa o formulário
    initializeSignatureForm();
    
    setTimeout(function() {
        // Gera assinatura
        pki.signHash({
            thumbprint: $('#SelectedCertThumbprintField').val(),
            hash: $('#ToSignHashField').val(),
            digestAlgorithm: $('#DigestAlgorithmField').val()
        }).success(function(signature) {
            // Armazena assinatura e envia para o servidor
            $('#SignatureField').val(signature);
            $('#SubmitSignatureButton').click();
        });
    }, 1000);
}
```

**Backend:**
```csharp
protected void SubmitSignatureButton_Click(object sender, EventArgs e)
{
    try
    {
        // Recupera dados da sessão
        var transferData = Session[TransferDataSessionKey] as byte[];
        string filePath = Session["SignedFilePath"] as string;
        
        // Cria assinador PAdES
        var padesSigner = new PadesSigner();
        padesSigner.SetPolicy(getSignaturePolicy());
        
        // Define assinatura pré-computada
        padesSigner.SetPreComputedSignature(
            Convert.FromBase64String(SignatureField.Value), 
            transferData
        );
        
        // Computa assinatura final
        padesSigner.ComputeSignature();
        
        // Salva PDF assinado
        var signedPdf = padesSigner.GetPadesSignature();
        SaveSignedFile(signedPdf);
        
        // Limpa dados temporários
        CleanupTemporaryFiles();
    }
    catch (Exception ex)
    {
        // Tratamento de erro
    }
}
```

### Gerenciamento de Estado

#### Session Storage
```csharp
// Chaves de sessão utilizadas
private const string UploadedFileSessionKey = "UploadedFile";
private const string TransferDataSessionKey = "TransferData";
```

#### Campos Ocultos
```html
<asp:HiddenField ID="CertificateField" runat="server" />
<asp:HiddenField ID="ToSignHashField" runat="server" />
<asp:HiddenField ID="DigestAlgorithmField" runat="server" />
<asp:HiddenField ID="SignatureField" runat="server" />
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
try
{
    // Operações de assinatura
}
catch (ValidationException ex)
{
    lblMessage.Text = string.Join("<br/>", ex.ValidationResults.Errors.Select(ve => ve.ToString()));
    lblMessage.CssClass = "message error";
}
catch (Exception ex)
{
    lblMessage.Text = "Error: " + ex.Message;
    lblMessage.CssClass = "message error";
}
```

## Gerenciamento de Estado

O projeto utiliza várias técnicas para manter o estado durante o processo:

1. **Session Storage**
   - Armazena dados do certificado
   - Mantém informações do arquivo
   - Preserva dados de transferência

2. **Campos Ocultos**
   - `CertificateField`: Certificado selecionado
   - `ToSignHashField`: Hash para assinatura
   - `DigestAlgorithmField`: Algoritmo de digest
   - `SignatureField`: Assinatura gerada
   - `SelectedCertThumbprintField`: Thumbprint do certificado

3. **UpdatePanels**
   - `UploadPanel`: Controle de upload
   - `CertificatePanel`: Seleção de certificado
   - `SignaturePanel`: Botão de completar assinatura

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
   - Validação de tipos de arquivo
   - Nomes de arquivo seguros

## Tratamento de Erros

O sistema implementa tratamento de erros em vários níveis:

1. **Cliente**
   - Validação de certificado selecionado
   - Verificação de arquivo PDF
   - Erros de Web PKI

2. **Servidor**
   - Validação de certificado
   - Processamento de arquivo
   - Geração de assinatura

3. **Feedback ao Usuário**
   - Mensagens de erro claras
   - Logs detalhados no console
   - Indicadores visuais de progresso

## Dependências

- **Web PKI**: Componente de assinatura digital
- **jQuery**: Manipulação DOM e AJAX
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