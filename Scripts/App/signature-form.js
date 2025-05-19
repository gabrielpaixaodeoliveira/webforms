// ----------------------------------------------------------------------------------------------------------
// This file contains logic for calling the Web PKI component to perform a signature. It is only an example,
// feel free to alter it to meet your application's needs.
// ----------------------------------------------------------------------------------------------------------
var signatureForm = (function () {

	// Auxiliary global variables.
	var formElements = {};
	var selectedCertThumbprint = null;
	var pki = null;

	// ------------------------------------------------------------------------------------------------------
	// Function called once the page is loaded or once the update panel with the hidden fields used to pass
	// data to and from the code-behind is updated.
	// ------------------------------------------------------------------------------------------------------
	function pageLoad(fe) {

		// We update our references to the form elements everytime this function is called, since the
		// elements change when the UpdatePanel is updated.
		formElements = fe;

		if (pki === null) {
			// If the Web PKI component is not initialized that means this is the initial load of the page
			// (not a refresh of the update panel). Therefore, we initialize the Web PKI component and list
			// the user's certificates.
			initPki();
		} else if (formElements.toSignHashField.val()) {
			// If the Web PKI is already initialized, this is a refresh of the update panel. If the hidden
			// field "toSignHash" was filled on the code-behind, we go ahead and sign it.
			sign();
		} else {
			// If the Web PKI is already initialized but the hidden field "toSignHash" is empty, this is
			// a refresh of the update panel but the signature could not be initiated on the code-behind
			// (probably because of a validation error). Therefore, we just unblock the UI (which is was
			// previously blocked by the sign() function).
			$.unblockUI();
		}
	}

	// ------------------------------------------------------------------------------------------------------
	// Function that initializes the Web PKI component, called on the first load of the page.
	// ------------------------------------------------------------------------------------------------------
	function initPki() {

		// Block the UI while we get things ready.
		$.blockUI({ message: 'Initializing ...' });

		try {
			// Create an instance of the Lacuna object.
			pki = new LacunaWebPKI();

			// Call the init() method on the LacunaWebPKI object
			pki.init({
				ready: function() {
					// As soon as the component is ready we'll load the certificates
					loadCertificates();
				},
				defaultError: function(message, error, origin) {
					// Unblock the UI
					$.unblockUI();
					// Log the error
					console.error('Web PKI Error:', message, error, origin);
					// Show user-friendly error
					alert('Error initializing digital signature component: ' + message);
				}
			});
		} catch (e) {
			$.unblockUI();
			console.error('Error creating Web PKI instance:', e);
			alert('Error initializing digital signature component. Please make sure the Web PKI component is properly installed.');
		}
	}

	// ------------------------------------------------------------------------------------------------------
	// Function called when the user clicks the "Refresh" button.
	// ------------------------------------------------------------------------------------------------------
	function refresh() {
		// Block the UI while we load the certificates.
		$.blockUI();
		// Invoke the loading of the certificates.
		loadCertificates();
	}

	// ------------------------------------------------------------------------------------------------------
	// Function that loads the certificates, either on startup or when the user clicks the "Refresh" button.
	// At this point, the UI is already blocked.
	// ------------------------------------------------------------------------------------------------------
	function loadCertificates() {

		// Call the listCertificates() method to list the user's certificates. For more information see:
		// http://webpki.lacunasoftware.com/Help/classes/LacunaWebPKI.html#method_listCertificates
		pki.listCertificates({

			// The ID of the <select> element to be populated with the certificates.
			selectId: formElements.certificateSelect.attr('id'),

			// Function that will be called to get the text that should be displayed for each option.
			selectOptionFormatter: function (cert) {
				var s = cert.subjectName + ' (issued by ' + cert.issuerName + ')';
				if (new Date() > cert.validityEnd) {
					s = '[EXPIRED] ' + s;
				}
				return s;
			}

		}).success(function () {

			// Once the certificates have been listed, unblock the UI.
			$.unblockUI();

		});
	}

	// ------------------------------------------------------------------------------------------------------
	// Function called when the user clicks the "Sign File" button.
	// ------------------------------------------------------------------------------------------------------
	function startSignature() {
		console.log('startSignature called');
		// Block the UI while we perform the signature.
		$.blockUI({ message: 'Signing ...' });

		try {
			// Get the value attribute of the option selected on the dropdown
			selectedCertThumbprint = formElements.certificateSelect.val();
			console.log('Selected certificate thumbprint:', selectedCertThumbprint);
			if (!selectedCertThumbprint) {
				$.unblockUI();
				alert('Please select a certificate first.');
				return;
			}

			// Read the selected certificate's encoding.
			console.log('Reading certificate...');
			pki.readCertificate(selectedCertThumbprint).success(function (certEncoded) {
				console.log('Certificate read successfully, length:', certEncoded.length);
				// Fill the hidden field "certificateField" with the certificate encoding
				formElements.certificateField.val(certEncoded);
				console.log('Certificate field value set:', formElements.certificateField.val() ? 'has value' : 'empty');

				// Fire up the click event of the button "SubmitCertificateButton"
				console.log('Clicking SubmitCertificateButton');
				formElements.submitCertificateButton.click();
			}).fail(function(error) {
				$.unblockUI();
				console.error('Error reading certificate:', error);
				alert('Error reading certificate. Please try again.');
			});
		} catch (e) {
			$.unblockUI();
			console.error('Error starting signature process:', e);
			alert('Error starting signature process. Please try again.');
		}
	}

	// ------------------------------------------------------------------------------------------------------
	// Function that signs "toSignHash" computed on the code-behind.
	// ------------------------------------------------------------------------------------------------------
	function sign() {
		if (!pki) {
			alert('Digital signature component is not initialized. Please try refreshing the page.');
			$.unblockUI();
			return;
		}

		try {
			if (!selectedCertThumbprint) {
				$.unblockUI();
				alert('No certificate selected. Please try again.');
				return;
			}

			if (!formElements.toSignHashField.val() || !formElements.digestAlgorithmField.val()) {
				$.unblockUI();
				alert('Missing signature data. Please try again.');
				return;
			}

			// Call Web PKI passing the selected certificate, the document's "to sign hash" and the digest
			// algorithm to be used during the signature algorithm.
			pki.signHash({
				thumbprint: selectedCertThumbprint,
				hash: formElements.toSignHashField.val(),
				digestAlgorithm: formElements.digestAlgorithmField.val()
			}).success(function (signature) {
				// Fill the hidden field "signatureField" with the result of the signature algorithm.
				formElements.signatureField.val(signature);
				// Fire up the click event of the button "SubmitSignatureButton"
				formElements.submitSignatureButton.click();
			}).fail(function (error) {
				$.unblockUI();
				console.error('Signature error:', error);
				alert('Error performing signature: ' + (error.message || 'Unknown error'));
			});
		} catch (e) {
			$.unblockUI();
			console.error('Error during signature process:', e);
			alert('Error during signature process. Please try again.');
		}
	}

	// ------------------------------------------------------------------------------------------------------
	// Function called if an error occurs on the Web PKI component.
	// ------------------------------------------------------------------------------------------------------
	function onWebPkiError(message, error, origin) {

		// Unblock the UI.
		$.unblockUI();
		// Log the error to the browser console (for debugging purposes).
		if (console) {
			console.log('An error has occurred on the signature browser component: ' + message, error);
		}
		// Show the message to the user. You might want to substitute the alert below with a more
		// user-friendly UI component to show the error.
		alert(message);
	}

	// ------------------------------------------------------------------------------------------------------
	// Handling of errors in the UpdatePanel refresh.
	// ------------------------------------------------------------------------------------------------------
	if (Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
		Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function (sender, args) {
			if (args.get_error()) {
				alert('An error has occurred on the server');
				$.unblockUI();
			}
		});
	}

	return {
		pageLoad: pageLoad,
		refresh: refresh,
		startSignature: startSignature,
		sign: sign  // Expose the sign function
	};

})();
