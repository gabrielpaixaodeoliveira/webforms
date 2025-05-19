using System;
using System.IO;
using System.Web;
using System.Web.UI;
using System.Linq;
using Lacuna.Pki;
using Lacuna.Pki.Pades;
using Lacuna.Pki.Stores;
using System.Security.Cryptography.X509Certificates;
using Lacuna.Pki.Cades;
using static Lacuna.Pki.Cades.CadesPolicySpec;
using System.Web.UI.WebControls;
using System.Text;

namespace WebForms
{
    public partial class Default : System.Web.UI.Page
    {
        private const string UploadedFileSessionKey = "UploadedFile";
        private const string TransferDataSessionKey = "TransferData";

        // Add field declarations
        protected HiddenField CertificateField;
        protected HiddenField ToSignHashField;
        protected HiddenField DigestAlgorithmField;
        protected HiddenField SignatureField;
        protected HiddenField TransferDataFileIdField;
        protected HiddenField SelectedCertThumbprintField;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Carregando licença
            PkiConfig.LoadLicense($"{Server.MapPath(".")}\\LacunaPkiLicense.config");

            if (!IsPostBack)
            {
                // Initialize the page
                lblMessage.Text = "";

                // Create necessary directories
                string uploadsPath = Server.MapPath("~/Uploads");
                string tempPath = Server.MapPath("~/Temp");
                string signedPath = Server.MapPath("~/Signed");

                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);
                if (!Directory.Exists(tempPath))
                    Directory.CreateDirectory(tempPath);
                if (!Directory.Exists(signedPath))
                    Directory.CreateDirectory(signedPath);
            }
            else
            {
                // Restore certificate from session if exists
                if (Session["SelectedCertificate"] != null)
                {
                    CertificateField.Value = Session["SelectedCertificate"].ToString();
                }
                if (Session["SelectedCertThumbprint"] != null)
                {
                    SelectedCertThumbprintField.Value = Session["SelectedCertThumbprint"].ToString();
                }
            }
        }

        protected void SubmitCertificateButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(CertificateField.Value))
                {
                    lblMessage.Text = "Please select a certificate";
                    lblMessage.CssClass = "message error";
                    SignaturePanel.Update();
                    return;
                }

                // Store the certificate in session
                Session["SelectedCertificate"] = CertificateField.Value;
                Session["SelectedCertThumbprint"] = SelectedCertThumbprintField.Value;

                // First handle the file upload if there's a file
                if (FileUpload1.HasFile)
                {
                    HttpPostedFile uploadedFile = FileUpload1.PostedFile;

                    // Verify if it's a PDF file
                    if (!uploadedFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        lblMessage.Text = "Please select only PDF files.";
                        lblMessage.CssClass = "message error";
                        return;
                    }

                    // Save the file to temp directory
                    string tempPath = Path.Combine(Server.MapPath("~/Temp"), Path.GetRandomFileName());
                    uploadedFile.SaveAs(tempPath);

                    // Store the file path in session
                    Session[UploadedFileSessionKey] = tempPath;
                    Session["FileName"] = uploadedFile.FileName;

                    // Clear any existing session data
                    Session.Remove(TransferDataSessionKey);
                    Session.Remove("SignedFilePath");
                }

                // Get the file path from session
                string filePath = Session[UploadedFileSessionKey] as string;
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    lblMessage.Text = "Please select a file to sign.";
                    lblMessage.CssClass = "message error";
                    return;
                }

                // Validate certificate
                if (string.IsNullOrEmpty(CertificateField.Value))
                {
                    lblMessage.Text = "No certificate was selected. Please select a certificate and try again.";
                    lblMessage.CssClass = "message error";
                    return;
                }

                try
                {
                    // Decode the user's certificate
                    var cert = PKCertificate.Decode(Convert.FromBase64String(CertificateField.Value));

                    // Instantiate a PadesSigner class
                    var padesSigner = new PadesSigner();

                    // Set the PDF to sign
                    padesSigner.SetPdfToSign(File.ReadAllBytes(filePath));

                    // Set the signer certificate
                    padesSigner.SetSigningCertificate(cert);

                    // Set the signature policy
                    padesSigner.SetPolicy(getSignaturePolicy());

                    // Set the signature's visual representation
                    padesSigner.SetVisualRepresentation(getVisualRepresentation(cert));

                    // Generate the to-sign bytes and transfer data
                    byte[] toSignBytes = padesSigner.GetToSignBytes(out SignatureAlgorithm signatureAlg, out byte[] transferData);

                    // Store the transfer data in session
                    Session[TransferDataSessionKey] = transferData;

                    // Store the file path in session for the next step
                    Session["SignedFilePath"] = filePath;

                    // Store the certificate in session for the next step
                    Session["SelectedCertificate"] = CertificateField.Value;

                    // Set the hash and algorithm for the client
                    ToSignHashField.Value = Convert.ToBase64String(signatureAlg.DigestAlgorithm.ComputeHash(toSignBytes));
                    DigestAlgorithmField.Value = signatureAlg.DigestAlgorithm.Oid;

                    // The library will automatically call sign() when these fields are set
                    lblMessage.Text = "Certificate selected successfully. Please complete the signature.";
                    lblMessage.CssClass = "message success";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing certificate: {ex}");
                    throw new Exception("Error processing certificate. Please try again.");
                }

                // Update the signature panel to show the complete button
                SignaturePanel.Update();
            }
            catch (ValidationException ex)
            {
                lblMessage.Text = string.Join("<br/>", ex.ValidationResults.Errors.Select(ve => ve.ToString()));
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
            }
            catch (Exception ex)
            {
                lblMessage.Text = "Error: " + ex.Message;
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
            }
        }

        protected void SubmitSignatureButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(SignatureField.Value))
                {
                    lblMessage.Text = "Signature is missing";
                    lblMessage.CssClass = "message error";
                    SignaturePanel.Update();
                    return;
                }

                // Get the transfer data from session
                var transferData = Session[TransferDataSessionKey] as byte[];
                if (transferData == null)
                {
                    throw new Exception("Transfer data not found. Please try again.");
                }

                // Get the file path from session
                string filePath = Session["SignedFilePath"] as string;
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    throw new Exception("File not found. Please upload the file again.");
                }

                // Get the signature from the hidden field
                string signature = SignatureField.Value;
                if (string.IsNullOrEmpty(signature))
                {
                    throw new Exception("No signature was provided. Please try signing again.");
                }

                try
                {
                    // Create a PadesSigner instance
                    var padesSigner = new PadesSigner();

                    // Set the signature policy
                    padesSigner.SetPolicy(getSignaturePolicy());

                    // Set the pre-computed signature
                    padesSigner.SetPreComputedSignature(Convert.FromBase64String(signature), transferData);

                    // Compute the signature
                    padesSigner.ComputeSignature();

                    // Get the signed PDF
                    var signedPdf = padesSigner.GetPadesSignature();

                    // Save the signed PDF
                    string signedFilePath = Path.Combine(Server.MapPath("~/Signed"), 
                        Path.GetFileNameWithoutExtension(Session["FileName"] as string) + "_signed.pdf");
                    File.WriteAllBytes(signedFilePath, signedPdf);

                    // Clean up
                    File.Delete(filePath);
                    Session.Remove(TransferDataSessionKey);
                    Session.Remove("SignedFilePath");
                    Session.Remove(UploadedFileSessionKey);
                    Session.Remove("FileName");

                    // Show success message with download link
                    lblMessage.Text = "File signed successfully! <a href='Signed/" + Path.GetFileName(signedFilePath) + "' target='_blank'>Download signed file</a>";
                    lblMessage.CssClass = "message success";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during signature computation: {ex}");
                    throw new Exception("Error during signature computation. Please try again.");
                }

                // Update all panels to show the final state
                UploadPanel.Update();
                CertificatePanel.Update();
                SignaturePanel.Update();
            }
            catch (ValidationException ex)
            {
                lblMessage.Text = string.Join("<br/>", ex.ValidationResults.Errors.Select(ve => ve.ToString()));
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
            }
            catch (Exception ex)
            {
                lblMessage.Text = "Error: " + ex.Message;
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
            }
        }

        private IPadesPolicyMapper getSignaturePolicy()
        {
            return PadesPoliciesForGeneration.GetPadesBasic(Util.GetTrustArbitrator());
        }

        private PadesVisualRepresentation2 getVisualRepresentation(PKCertificate cert)
        {
            var visualRepresentation = new PadesVisualRepresentation2()
            {
                Text = new PadesVisualText()
                {
                    CustomText = String.Format("Signed by {0}", cert.SubjectName.CommonName),
                    IncludeSigningTime = true,
                    HorizontalAlign = PadesTextHorizontalAlign.Left,
                    Container = new PadesVisualRectangle()
                    {
                        Left = 0.2,
                        Top = 0.2,
                        Right = 0.2,
                        Bottom = 0.2
                    }
                }
            };

            var visualPositioning = PadesVisualAutoPositioning.GetFootnote();
            visualPositioning.Container.Height = 4.94;
            visualPositioning.SignatureRectangleSize.Width = 8.0;
            visualPositioning.SignatureRectangleSize.Height = 4.94;
            visualRepresentation.Position = visualPositioning;

            return visualRepresentation;
        }

        protected void btnListCerts_Click(object sender, EventArgs e)
        {
            try
            {
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Certificates found in store:");
                
                foreach (var cert in store.Certificates)
                {
                    sb.AppendLine($"Subject: {cert.Subject}");
                    sb.AppendLine($"Issuer: {cert.Issuer}");
                    sb.AppendLine($"Valid until: {cert.NotAfter}");
                    sb.AppendLine($"Has private key: {cert.HasPrivateKey}");
                    sb.AppendLine("---");
                }

                lblMessage.Text = sb.ToString().Replace("\n", "<br/>");
                lblMessage.CssClass = "message";
            }
            catch (Exception ex)
            {
                lblMessage.Text = $"Error listing certificates: {ex.Message}";
                lblMessage.CssClass = "message error";
            }
        }

        protected void btnTestCertificates_Click(object sender, EventArgs e)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<h3>Certificate Store Information</h3>");

                // Test Windows Store
                var store = WindowsCertificateStore.LoadPersonalCurrentUser();
                var certsWithKey = store.GetCertificatesWithKey();

                sb.AppendLine($"<p>Total certificates with key: {certsWithKey.Count()}</p>");
                sb.AppendLine("<ul>");

                foreach (var cert in certsWithKey)
                {
                    sb.AppendLine("<li>");
                    sb.AppendLine($"<strong>Subject:</strong> {cert.Certificate.SubjectName}<br/>");
                    sb.AppendLine($"<strong>Issuer:</strong> {cert.Certificate.IssuerName}<br/>");
                    sb.AppendLine($"<strong>Valid until:</strong> {cert.Certificate.ValidityEnd}<br/>");
                    sb.AppendLine($"<strong>Serial Number:</strong> {cert.Certificate.SerialNumber}<br/>");
                    sb.AppendLine("</li>");
                }

                sb.AppendLine("</ul>");

                lblMessage.Text = sb.ToString();
                lblMessage.CssClass = "message";
            }
            catch (Exception ex)
            {
                lblMessage.Text = $"Error testing certificates: {ex.Message}";
                lblMessage.CssClass = "message error";
            }
        }
    }

    // Add this class to handle certificate trust
    public class CustomTrustArbitrator : ITrustArbitrator
    {
        public bool IsRootTrusted(PKCertificate certificate, DateTimeOffset? validationTime, out ValidationResults validationResults)
        {
            // Accept all certificates
            validationResults = new ValidationResults();
            return true;
        }

        public ICertificateStore GetCertificateStore()
        {
            return WindowsCertificateStore.LoadPersonalCurrentUser();
        }
    }
}