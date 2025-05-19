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
using System.Drawing;

namespace WebForms
{
    /// <summary>
    /// Main page for file upload and digital signature functionality
    /// </summary>
    public partial class Default : System.Web.UI.Page
    {
        #region Constants and Fields
        private const string UploadedFileSessionKey = "UploadedFile";
        private const string TransferDataSessionKey = "TransferData";
        private const string SelectedCertificateSessionKey = "SelectedCertificate";
        private const string SelectedCertThumbprintSessionKey = "SelectedCertThumbprint";
        private const string SignedFilePathSessionKey = "SignedFilePath";
        private const string FileNameSessionKey = "FileName";

        // UI Controls
        protected HiddenField CertificateField;
        protected HiddenField ToSignHashField;
        protected HiddenField DigestAlgorithmField;
        protected HiddenField SignatureField;
        protected HiddenField TransferDataFileIdField;
        protected HiddenField SelectedCertThumbprintField;
        #endregion

        #region Page Lifecycle
        protected void Page_Load(object sender, EventArgs e)
        {
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
        #endregion

        #region Event Handlers
        protected void SubmitCertificateButton_Click(object sender, EventArgs e)
        {
            try
            {
                ValidateCertificateSelection();
                HandleFileUpload();
                ProcessCertificateAndGenerateSignature();
                UpdateUIForSignature();
            }
            catch (Exception ex)
            {
                HandleGeneralError(ex);
            }
        }

        protected void SubmitSignatureButton_Click(object sender, EventArgs e)
        {
            try
            {
                ValidateSignatureSubmission();
                ProcessSignatureAndSaveFile();
                CleanupAndShowSuccess();
            }
            catch (Exception ex)
            {
                HandleGeneralError(ex);
            }
        }
        #endregion

        #region Private Methods
        private void InitializePkiLicense()
        {
            PkiConfig.LoadLicense($"{Server.MapPath(".")}\\LacunaPkiLicense.config");
        }

        private void EnsureDirectoriesExist()
        {
            string[] directories = new[]
            {
                Server.MapPath("~/Uploads"),
                Server.MapPath("~/Temp"),
                Server.MapPath("~/Signed")
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private void InitializePage()
        {
            lblMessage.Text = "";
        }

        private void RestoreCertificateFromSession()
        {
            if (Session[SelectedCertificateSessionKey] != null)
            {
                CertificateField.Value = Session[SelectedCertificateSessionKey].ToString();
            }
            if (Session[SelectedCertThumbprintSessionKey] != null)
            {
                SelectedCertThumbprintField.Value = Session[SelectedCertThumbprintSessionKey].ToString();
            }
        }

        private void ValidateCertificateSelection()
        {
            if (string.IsNullOrEmpty(CertificateField.Value))
            {
                lblMessage.Text = "Please select a certificate";
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
                throw new Exception("Please select a certificate");
            }
        }

        private void HandleFileUpload()
        {
            if (FileUpload1.HasFile)
            {
                ValidateAndSaveUploadedFile();
            }
        }

        private void ValidateAndSaveUploadedFile()
        {
            HttpPostedFile uploadedFile = FileUpload1.PostedFile;

            if (!uploadedFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                lblMessage.Text = "Please select only PDF files.";
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
                throw new Exception("Please select only PDF files.");
            }

            string tempPath = Path.Combine(Server.MapPath("~/Temp"), Path.GetRandomFileName());
            uploadedFile.SaveAs(tempPath);

            Session[UploadedFileSessionKey] = tempPath;
            Session[FileNameSessionKey] = uploadedFile.FileName;

            // Clear existing session data
            Session.Remove(TransferDataSessionKey);
            Session.Remove(SignedFilePathSessionKey);
        }

        private void ProcessCertificateAndGenerateSignature()
        {
            string filePath = GetAndValidateFilePath();
            var cert = DecodeCertificate();
            var signatureData = GenerateSignatureData(filePath, cert);
            StoreSignatureData(signatureData);
        }

        private string GetAndValidateFilePath()
        {
            string filePath = Session[UploadedFileSessionKey] as string;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                lblMessage.Text = "Please select a file to sign.";
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
                throw new Exception("Please select a file to sign.");
            }
            return filePath;
        }

        private PKCertificate DecodeCertificate()
        {
            return PKCertificate.Decode(Convert.FromBase64String(CertificateField.Value));
        }

        private (byte[] toSignBytes, SignatureAlgorithm signatureAlg, byte[] transferData) GenerateSignatureData(string filePath, PKCertificate cert)
        {
            var padesSigner = new PadesSigner();
            padesSigner.SetPdfToSign(File.ReadAllBytes(filePath));
            padesSigner.SetSigningCertificate(cert);
            padesSigner.SetPolicy(getSignaturePolicy());
            padesSigner.SetVisualRepresentation(getVisualRepresentation(cert));

            byte[] toSignBytes = padesSigner.GetToSignBytes(out SignatureAlgorithm signatureAlg, out byte[] transferData);
            return (toSignBytes, signatureAlg, transferData);
        }

        private void StoreSignatureData((byte[] toSignBytes, SignatureAlgorithm signatureAlg, byte[] transferData) signatureData)
        {
            Session[TransferDataSessionKey] = signatureData.transferData;
            Session[SignedFilePathSessionKey] = Session[UploadedFileSessionKey];
            Session[SelectedCertificateSessionKey] = CertificateField.Value;

            ToSignHashField.Value = Convert.ToBase64String(signatureData.signatureAlg.DigestAlgorithm.ComputeHash(signatureData.toSignBytes));
            DigestAlgorithmField.Value = signatureData.signatureAlg.DigestAlgorithm.Oid;
        }

        private void UpdateUIForSignature()
        {
            lblMessage.Text = "Certificate selected successfully. Please complete the signature.";
            lblMessage.CssClass = "message success";
            SignaturePanel.Update();
        }

        private void ValidateSignatureSubmission()
        {
            if (string.IsNullOrEmpty(SignatureField.Value))
            {
                lblMessage.Text = "Signature is missing";
                lblMessage.CssClass = "message error";
                SignaturePanel.Update();
                throw new Exception("Signature is missing");
            }
        }

        private void ProcessSignatureAndSaveFile()
        {
            var transferData = GetTransferData();
            string filePath = GetAndValidateFilePath();
            string signature = SignatureField.Value;

            var signedPdf = GenerateSignedPdf(transferData, signature);
            SaveSignedPdf(signedPdf);
        }

        private byte[] GetTransferData()
        {
            var transferData = Session[TransferDataSessionKey] as byte[];
            if (transferData == null)
            {
                throw new Exception("Transfer data not found. Please try again.");
            }
            return transferData;
        }

        private byte[] GenerateSignedPdf(byte[] transferData, string signature)
        {
            var padesSigner = new PadesSigner();
            padesSigner.SetPolicy(getSignaturePolicy());
            padesSigner.SetPreComputedSignature(Convert.FromBase64String(signature), transferData);
            padesSigner.ComputeSignature();
            return padesSigner.GetPadesSignature();
        }

        private void SaveSignedPdf(byte[] signedPdf)
        {
            string signedFilePath = Path.Combine(Server.MapPath("~/Signed"),
                Path.GetFileNameWithoutExtension(Session[FileNameSessionKey] as string) + "_signed.pdf");
            File.WriteAllBytes(signedFilePath, signedPdf);
        }

        private void CleanupAndShowSuccess()
        {
            // Store file name before clearing session
            string originalFileName = Session[FileNameSessionKey] as string;
            string signedFileName = Path.GetFileNameWithoutExtension(originalFileName) + "_signed.pdf";
            string signedFilePath = Path.Combine(Server.MapPath("~/Signed"), signedFileName);

            // Clean up temporary file
            string tempFilePath = Session[SignedFilePathSessionKey] as string;
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            // Verify signed file exists before creating download link
            if (File.Exists(signedFilePath))
            {
                // Show success message with download link
                lblMessage.Text = $"File signed successfully! <a href='Signed/{signedFileName}' target='_blank'>Download signed file</a>";
            }
            else
            {
                lblMessage.Text = "File signed successfully, but there was an error creating the download link.";
            }
            lblMessage.CssClass = "message success";
            SignaturePanel.Update();

            // Clear session data after creating the message
            Session.Remove(TransferDataSessionKey);
            Session.Remove(SignedFilePathSessionKey);
            Session.Remove(UploadedFileSessionKey);
            Session.Remove(FileNameSessionKey);
        }

        private void HandleGeneralError(Exception ex)
        {
            lblMessage.Text = "Error: " + ex.Message;
            lblMessage.CssClass = "message error";
            SignaturePanel.Update();
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
        #endregion
    }

    /// <summary>
    /// Custom trust arbitrator for certificate validation
    /// </summary>
    public class CustomTrustArbitrator : ITrustArbitrator
    {
        public bool IsRootTrusted(PKCertificate certificate, DateTimeOffset? validationTime, out ValidationResults validationResults)
        {
            validationResults = new ValidationResults();
            return true;
        }

        public ICertificateStore GetCertificateStore()
        {
            return null;
        }
    }
}