<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="WebForms.Default" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>File Upload and Sign</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 20px;
        }
        .container {
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            border: 1px solid #ccc;
            border-radius: 5px;
        }
        .message {
            margin-top: 10px;
            padding: 10px;
            border-radius: 3px;
            display: block;
            white-space: pre-wrap;
            word-wrap: break-word;
            min-height: 20px;
            line-height: 1.5;
        }
        .success {
            background-color: #dff0d8;
            color: #3c763d;
        }
        .error {
            background-color: #f2dede;
            color: #a94442;
        }
        .options {
            margin: 15px 0;
        }
        .certificate-options {
            margin: 10px 0 10px 25px;
        }
    </style>
</head>
<body>
    <form id="form1" runat="server" enctype="multipart/form-data">
        <asp:ScriptManager ID="ScriptManager1" runat="server">
            <Scripts>
                <asp:ScriptReference Path="~/Scripts/jquery-3.1.1.min.js" />
                <asp:ScriptReference Path="~/Scripts/jquery.blockUI.js" />
                
                <asp:ScriptReference Path="~/Scripts/WebForms/MsAjax/MicrosoftAjax.js" />
                <asp:ScriptReference Path="~/Scripts/WebForms/MsAjax/MicrosoftAjaxApplicationServices.js" />
                <asp:ScriptReference Path="~/Scripts/WebForms/MsAjax/MicrosoftAjaxTimer.js" />
                <asp:ScriptReference Path="~/Scripts/WebForms/MsAjax/MicrosoftAjaxWebForms.js" />
                
                <asp:ScriptReference Path="~/Scripts/bootstrap.min.js" />
                <asp:ScriptReference Path="~/Scripts/lacuna-web-pki-2.11.0.js" />
                <asp:ScriptReference Path="~/Scripts/App/signature-form.js" />
            </Scripts>
        </asp:ScriptManager>

        <div class="container">
            <h2>File Upload and Sign</h2>
            
            <asp:UpdatePanel ID="UploadPanel" runat="server" UpdateMode="Conditional">
                <ContentTemplate>
                    <div>
                        <asp:FileUpload ID="FileUpload1" runat="server" />
                    </div>
                </ContentTemplate>
            </asp:UpdatePanel>

            <asp:UpdatePanel ID="CertificatePanel" runat="server" UpdateMode="Conditional">
                <ContentTemplate>
                    <div class="options">
                        <div class="form-group">
                            <label for="certificateSelect">Selecione um certificado</label>
                            <select id="certificateSelect" class="form-control"></select>
                        </div>
                    </div>
                    <div style="margin-top: 10px;">
                        <asp:Button ID="SignButton" runat="server" class="btn btn-primary" Text="Assinar Arquivo" OnClientClick="return submitCertificateAndSign();" />
                    </div>
                </ContentTemplate>
            </asp:UpdatePanel>

            <asp:UpdatePanel ID="SignaturePanel" runat="server" UpdateMode="Conditional">
                <ContentTemplate>
                    <div style="margin-top: 10px;">
                        <asp:Button ID="CompleteSignButton" runat="server" class="btn btn-success" Text="Completar Assinatura" OnClientClick="return generateAndSubmitSignature();" style="display: none;" />
                    </div>
                    <asp:Label ID="lblMessage" runat="server" CssClass="message"></asp:Label>
                </ContentTemplate>
            </asp:UpdatePanel>
            
            <!-- Hidden fields for signature process -->
            <asp:HiddenField ID="CertificateField" runat="server" />
            <asp:HiddenField ID="ToSignHashField" runat="server" />
            <asp:HiddenField ID="DigestAlgorithmField" runat="server" />
            <asp:HiddenField ID="SignatureField" runat="server" />
            <asp:HiddenField ID="TransferDataFileIdField" runat="server" />
            <asp:HiddenField ID="SelectedCertThumbprintField" runat="server" />

            <!-- Hidden buttons for signature process -->
            <asp:Button ID="SubmitCertificateButton" runat="server" OnClick="SubmitCertificateButton_Click" style="display: none;" />
            <asp:Button ID="SubmitSignatureButton" runat="server" OnClick="SubmitSignatureButton_Click" style="display: none;" />
        </div>
    </form>

    <script type="text/javascript">
        var isSigning = false;
        var pki = null;

        function initializeSignatureForm() {
            console.log('Initializing signature form');
            // Initialize the signature form with all required elements
            signatureForm.pageLoad({
                certificateSelect: $('#certificateSelect'),
                submitCertificateButton: $('#<%= SubmitCertificateButton.ClientID %>'),
                submitSignatureButton: $('#<%= SubmitSignatureButton.ClientID %>'),
                certificateField: $('#<%= CertificateField.ClientID %>'),
                toSignHashField: $('#<%= ToSignHashField.ClientID %>'),
                digestAlgorithmField: $('#<%= DigestAlgorithmField.ClientID %>'),
                signatureField: $('#<%= SignatureField.ClientID %>')
            });

            // Add change event to store selected certificate
            $('#certificateSelect').on('change', function() {
                var thumbprint = $(this).val();
                console.log('Certificate selected:', thumbprint);
                $('#<%= SelectedCertThumbprintField.ClientID %>').val(thumbprint);
            });

            // Restore selected certificate if exists
            var savedThumbprint = $('#<%= SelectedCertThumbprintField.ClientID %>').val();
            if (savedThumbprint) {
                console.log('Restoring saved certificate:', savedThumbprint);
                $('#certificateSelect').val(savedThumbprint);
            }

            // Initialize Web PKI if not already initialized
            if (!pki) {
                console.log('Initializing Web PKI');
                pki = new LacunaWebPKI();
                pki.init({
                    ready: function() {
                        console.log('Web PKI initialized successfully');
                        // After initialization, ensure certificate is selected
                        var savedThumbprint = $('#<%= SelectedCertThumbprintField.ClientID %>').val();
                        if (savedThumbprint) {
                            console.log('Setting certificate after Web PKI init:', savedThumbprint);
                            $('#certificateSelect').val(savedThumbprint);
                        }
                    },
                    defaultError: function(message, error, origin) {
                        console.error('Web PKI Error:', message, error, origin);
                        alert('Error initializing digital signature component: ' + message);
                        $.unblockUI();
                        isSigning = false;
                    }
                });
            }
        }

        Sys.Application.add_init(function() {
            console.log('Sys.Application.add_init called');
            initializeSignatureForm();

            // Check if we have hash data after postback
            if ($('#<%= ToSignHashField.ClientID %>').val()) {
                console.log('ToSignHashField has value, showing complete button');
                $('#<%= CompleteSignButton.ClientID %>').show();
            }
        });

        function submitCertificateAndSign() {
            console.log('submitCertificateAndSign called');
            if (isSigning) return false;
            isSigning = true;
            
            // Store selected certificate before starting
            var thumbprint = $('#certificateSelect').val();
            console.log('Storing selected certificate:', thumbprint);
            $('#<%= SelectedCertThumbprintField.ClientID %>').val(thumbprint);
            
            // Call startSignature instead of directly clicking the button
            console.log('Calling startSignature');
            signatureForm.startSignature();
            return false;
        }

        function generateAndSubmitSignature() {
            console.log('generateAndSubmitSignature called');
            if (isSigning) return false;
            isSigning = true;

            // Reinitialize the form before signing
            initializeSignatureForm();
            
            // Wait a bit for initialization to complete
            setTimeout(function() {
                console.log('Calling sign after initialization');
                // Set the certificate field value from session
                var savedCert = $('#<%= CertificateField.ClientID %>').val();
                if (savedCert) {
                    console.log('Restored certificate from session');
                }

                // Ensure certificate is selected
                var thumbprint = $('#<%= SelectedCertThumbprintField.ClientID %>').val();
                if (!thumbprint) {
                    $.unblockUI();
                    isSigning = false;
                    return;
                }

                console.log('Using certificate thumbprint:', thumbprint);
                $('#certificateSelect').val(thumbprint);

                // Check if Web PKI is initialized
                if (!pki) {
                    console.log('Web PKI not initialized, initializing now');
                    pki = new LacunaWebPKI();
                    pki.init({
                        ready: function() {
                            console.log('Web PKI initialized, generating signature');
                            generateSignature();
                        },
                        defaultError: function(message, error, origin) {
                            console.error('Web PKI Error:', message, error, origin);
                            alert('Error initializing digital signature component: ' + message);
                            $.unblockUI();
                            isSigning = false;
                        }
                    });
                } else {
                    console.log('Web PKI already initialized, generating signature');
                    generateSignature();
                }
            }, 1000);
            
            return false;
        }

        function generateSignature() {
            console.log('Generating signature');
            var thumbprint = $('#<%= SelectedCertThumbprintField.ClientID %>').val();
            console.log('Using thumbprint for signature:', thumbprint);
            
            pki.signHash({
                thumbprint: thumbprint,
                hash: $('#<%= ToSignHashField.ClientID %>').val(),
                digestAlgorithm: $('#<%= DigestAlgorithmField.ClientID %>').val()
            }).success(function(signature) {
                console.log('Signature generated successfully');
                $('#<%= SignatureField.ClientID %>').val(signature);
                console.log('Submitting signature');
                $('#<%= SubmitSignatureButton.ClientID %>').click();
            }).fail(function(error) {
                console.error('Error generating signature:', error);
                alert('Error generating signature: ' + (error.message || 'Unknown error'));
                $.unblockUI();
                isSigning = false;
            });
        }

        // Handle postback errors
        if (Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
            Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function (sender, args) {
                if (args.get_error()) {
                    console.error('Postback error:', args.get_error());
                    alert('An error has occurred on the server');
                    $.unblockUI();
                    isSigning = false;
                } else {
                    console.log('Postback completed successfully');
                    // Reinitialize everything after postback
                    initializeSignatureForm();
                    
                    if ($('#<%= ToSignHashField.ClientID %>').val()) {
                        console.log('ToSignHashField has value after postback');
                        $('#<%= CompleteSignButton.ClientID %>').show();
                    }
                }
            });
        }
    </script>
</body>
</html> 