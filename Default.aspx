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
    <form id="form1" runat="server">
        <div class="container">
            <h2>File Upload and Sign</h2>
            <div>
                <asp:FileUpload ID="FileUpload1" runat="server" />
            </div>
            <div class="options">
                <div id="certificateOptions" runat="server" class="certificate-options">
                    <div style="margin-bottom: 10px;">
                        <asp:Label ID="lblCertificate" runat="server" Text="Select Certificate:" AssociatedControlID="ddlCertificates" />
                    </div>
                    <asp:DropDownList ID="ddlCertificates" runat="server" Width="100%" />
                </div>
            </div>
            <div style="margin-top: 10px;">
                <asp:Button ID="btnUpload" runat="server" Text="Upload File" OnClick="btnUpload_Click" />
            </div>
            <asp:Label ID="lblMessage" runat="server" CssClass="message"></asp:Label>
        </div>
    </form>
</body>
</html> 