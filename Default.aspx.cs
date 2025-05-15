using System;
using System.IO;
using System.Web.UI;
using System.Linq;
using Lacuna.Pki;
using Lacuna.Pki.Pades;
using Lacuna.Pki.Stores;
using System.Security.Cryptography.X509Certificates;
using Lacuna.Pki.Cades;
using static Lacuna.Pki.Cades.CadesPolicySpec;
using System.Web.UI.WebControls;

namespace WebForms
{
    public partial class Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Carregando licença
            PkiConfig.LoadLicense($"{Server.MapPath(".")}\\LacunaPkiLicense.config");

            if (!IsPostBack)
            {
                // Create uploads directory if it doesn't exist
                string uploadsPath = Server.MapPath("~/Uploads");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Load certificates into dropdown
                LoadCertificates();
            }
        }

        private void LoadCertificates()
        {
            // Clear existing items
            ddlCertificates.Items.Clear();

            var store = WindowsCertificateStore.LoadPersonalCurrentUser();
            var certsWithKey = store.GetCertificatesWithKey();

            foreach (var cert in certsWithKey)
            {
                if (cert != null)
                {
                    var item = new ListItem(
                        $"{cert.Certificate.SubjectName} (Valid until: {cert.Certificate.ValidityEnd.DateTime.ToShortDateString()})",
                        cert.Certificate.SerialNumber.ToString()
                    );
                    ddlCertificates.Items.Add(item);
                }
            }

            if (ddlCertificates.Items.Count == 0)
            {
                lblMessage.Text = "No valid certificates could be loaded.";
                lblMessage.CssClass = "message error";
            }
            else
            {
                lblMessage.Text = $"Loaded {ddlCertificates.Items.Count} certificates successfully.";
                lblMessage.CssClass = "message success";
            }
        }    

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            if (FileUpload1.HasFile)
            {
                try
                {
                    string fileName = Path.GetFileName(FileUpload1.FileName);
                    string uploadsPath = Server.MapPath("~/Uploads");
                    string filePath = Path.Combine(uploadsPath, fileName);
                    
                    // Save the uploaded file
                    FileUpload1.SaveAs(filePath);

                        if (string.IsNullOrEmpty(ddlCertificates.SelectedValue))
                        {
                            lblMessage.Text = "Please select a certificate to sign the document.";
                            lblMessage.CssClass = "message error";
                            return;
                        }

                        // Load the PDF file
                        var pdfBytes = File.ReadAllBytes(filePath);

                        try
                        {
                            // Load the selected certificate from Windows store
                            var store = WindowsCertificateStore.LoadPersonalCurrentUser();
                            var certsWithKey = store.GetCertificatesWithKey();
                            var selectedCert = certsWithKey.FirstOrDefault(c => 
                                c.Certificate.SerialNumber.ToString() == ddlCertificates.SelectedValue);

                            if (selectedCert == null)
                            {
                                lblMessage.Text = "Selected certificate not found.";
                                lblMessage.CssClass = "message error";
                                return;
                            }

                            // Configure the signature with policy
                            var signer = new CadesSigner();
                            var policy = BrazilCadesPolicyMappers.GetAdrBasica(false);
                            signer.SetDataToSign(pdfBytes);
                            signer.SetPolicy(policy);
                            signer.SetSigningCertificate(selectedCert);

                            // Compute the signature
                            signer.ComputeSignature();
                            var cadesSig = signer.GetSignature();

                            // Save the signed file
                            string signedFileName = Path.GetFileNameWithoutExtension(fileName) + "_signed.p7s";
                            string signedFilePath = Path.Combine(uploadsPath, signedFileName);
                            File.WriteAllBytes(signedFilePath, cadesSig);

                            lblMessage.Text = $"File signed successfully! Signed file saved as: {signedFileName}";
                            lblMessage.CssClass = "message success";
                        }
                        catch(Exception ex)
                        {
                            lblMessage.Text = "Error signing document: " + ex.Message;
                            lblMessage.CssClass = "message error";
                        }
                }
                catch (Exception ex)
                {
                    lblMessage.Text = "Error: " + ex.Message;
                    lblMessage.CssClass = "message error";
                }
            }
            else
            {
                lblMessage.Text = "Please select a file to upload.";
                lblMessage.CssClass = "message error";
            }
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