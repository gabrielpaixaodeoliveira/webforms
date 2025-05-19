// Global variables
var isSigning = false;
var pki = null;

// Initialize the signature form with all required elements
function initializeSignatureForm() {
    console.log('Initializing signature form');
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

    initializeWebPKI();
}

// Initialize Web PKI component
function initializeWebPKI() {
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

// Handle certificate submission and signing
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

// Generate and submit signature
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
            initializeWebPKI();
        } else {
            console.log('Web PKI already initialized, generating signature');
            generateSignature();
        }
    }, 1000);
    
    return false;
}

// Generate signature using Web PKI
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

// Initialize the application
Sys.Application.add_init(function() {
    console.log('Sys.Application.add_init called');
    initializeSignatureForm();

    // Check if we have hash data after postback
    if ($('#<%= ToSignHashField.ClientID %>').val()) {
        console.log('ToSignHashField has value, showing complete button');
        $('#<%= CompleteSignButton.ClientID %>').show();
    }
});

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