using MySql.Data.MySqlClient;
using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ReportChecker {
    public partial class main : Form {
        bool connectionOk = false;
        dbMySql.dbMySql db;
        string reportUNC;
        Timer errorTimer;
        BindingSource mainBindingSource = new BindingSource();
        
        string conTurnoConInforme = "Con informe";
        string conTurnoSinInforme = "Sin informe";
        string protocoloIncorrecto = "¡Este turno posee un número de protocolo incorrecto!";
        string sinTurnoSinInforme = "No se econtró ningún turno";
        string sinTurnoConInforme = "A pesar de que no existe el turno en la base de datos, hay un informe almacenado con ese número de protocolo";

        string reportPath;
        string accessionNumber;
        int count;

        CheckBox check = new CheckBox();
        bool soloLectura = true;

        BackgroundWorker backgroundWorker;
        struct backgroundObject {
            public dbMySql.dbMySql db;
            public enum action { justConnect, searchByName, searchByID, searchByAccessionNumber };
            public action actionIs;
            public string name;
            public int id;
            public int accessionNumber;
        }
        backgroundObject bgwObj;

        public main() {
            InitializeComponent();
        }

        bool ConfigError;
        private void main_Load(object sender, EventArgs e) {
            var version = new Version(Application.ProductVersion);
            versionNumber.Text = "Versión: " + version.Major.ToString() + "." + version.Minor.ToString();

            errorTimer = new Timer();
            errorTimer.Interval = 1000;
            errorTimer.Tick += ErrorTimer_Tick;
            errorTimer.Enabled = true;

            check.Checked = true;
            check.Text = "Sólo lectura";
            check.CheckedChanged += setSoloLectura;

            ConfigError =
                string.IsNullOrWhiteSpace(Properties.Settings.Default.serverAddress)    ||
                string.IsNullOrWhiteSpace(Properties.Settings.Default.databaseName)     ||
                string.IsNullOrWhiteSpace(Properties.Settings.Default.databaseUsername) ||
                string.IsNullOrWhiteSpace(Properties.Settings.Default.serverPort)       ||
                string.IsNullOrWhiteSpace(Properties.Settings.Default.databasePassword) ||
                string.IsNullOrWhiteSpace(Properties.Settings.Default.reportUNC);
            if (ConfigError) return;

            db = new dbMySql.dbMySql();
            db.serverAddress = Properties.Settings.Default.serverAddress;
            db.serverPort = Properties.Settings.Default.serverPort;
            db.databaseName = Properties.Settings.Default.databaseName;
            db.databaseUsername = Properties.Settings.Default.databaseUsername;
            db.databasePassword = SSTCryptographer.Decrypt(
                    Properties.Settings.Default.databasePassword,
                    Properties.Settings.Default.databaseUsername +
                    "as56afs65qer3g654afg");
            db.limitResults = Properties.Settings.Default.limitResults;
            db.limitResultsTo = Properties.Settings.Default.limitResultsTo;
            db.useWildcard = Properties.Settings.Default.useWildcard;
            reportUNC = Properties.Settings.Default.reportUNC;

            db.CreateConnection();

            dataGridView1.DataSource = mainBindingSource;

            backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += databaseTransaction;
            backgroundWorker.RunWorkerCompleted += databaseTransactionResult;

            bgwObj = new backgroundObject();
            bgwObj.db = db;
            bgwObj.actionIs = backgroundObject.action.justConnect;
            backgroundWorker.RunWorkerAsync(bgwObj);
        }

        private void databaseTransactionResult(object sender, RunWorkerCompletedEventArgs e) {
            System.Diagnostics.Debug.WriteLine("databaseTransactionResult");
            toolStripProgressBar1.Visible = false;
            if (e.Error != null) {
                if (e.Error is MySql.Data.MySqlClient.MySqlException) {
                    int errorNumber = ((MySql.Data.MySqlClient.MySqlException)e.Error).Number;
                    System.Diagnostics.Debug.WriteLine(errorNumber);
                    System.Diagnostics.Debug.WriteLine(e.Error.Message);
                    System.Diagnostics.Debug.WriteLine(e.Error.Source);
                    if (errorNumber == 0 && e.Error.Message.Contains("Timeout expired.")) {
                        MessageBox.Show(string.Format("Se superó el tiempo de espera de la conexión.\n" +
                            "El tiempo actual es de {0}\n" +
                            "Aumente el tiempo de espera u optimize la base de datos.\n" +
                            "La consulta ha sido abortada.", 30),
                            "Tiempo de espera superado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    MySqlErrorHandler(errorNumber);

                    return;
                }
                if (e.Error is Exception) {
                    MessageBox.Show("Error, " + e.Error.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            } else {
                if (e.Result != null)
                    if (e.Result is DataTable) {
                        dataGridView1.SuspendLayout();
                        mainBindingSource.DataSource = e.Result;
                        dataGridView1.ResumeLayout();
                    }
                MySqlConnectionOk();
            }
        }

        private void databaseTransaction(object sender, DoWorkEventArgs e) {
            object result = new object();
            switch (((backgroundObject)e.Argument).actionIs) {
                case backgroundObject.action.justConnect:
                    ((backgroundObject)e.Argument).db.TestConnection();
                    break;
                case backgroundObject.action.searchByAccessionNumber:
                    int accessionNumber = ((backgroundObject)e.Argument).accessionNumber;
                    result = ((backgroundObject)e.Argument).db.SelectStudyByAccessionNumber(accessionNumber);
                    break;

                case backgroundObject.action.searchByID:
                    int patientID = ((backgroundObject)e.Argument).id;
                    result = ((backgroundObject)e.Argument).db.SelectStudyByID(patientID);
                    break;

                case backgroundObject.action.searchByName:
                    string patientName = ((backgroundObject)e.Argument).name;
                    result = ((backgroundObject)e.Argument).db.SelectStudyByName(patientName);
                    // the next statements are a crappy workarround to bypass MySqlData.DataAdapter.Fill 
                    // that by some reason even with a BackgroundWorker got the main UI thread stucked.
                    break;
            }
            e.Result = result;
        }

        private void setSoloLectura(object sender, EventArgs e) {
            soloLectura = check.Checked; 
        }

        static int refreshSeconds = 5;
        static int seconds = refreshSeconds;
        private void ErrorTimer_Tick(object sender, EventArgs e) {
            if (connectionOk == false) {
                if (--seconds == 0) {
                    seconds = refreshSeconds;
                    lblTryingToRecconect.Text = "Reconectando...";
                    try {
                        db.TestConnection();
                        MySqlConnectionOk();
                    }
                    catch (MySql.Data.MySqlClient.MySqlException error) { MySqlErrorHandler(error.Number); }
                } else {
                    lblTryingToRecconect.Text = "Intentando reconectar en " + seconds.ToString() + " segundos...";
                }
            } else {
                seconds = refreshSeconds;
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            if (ConfigError) {
                MessageBox.Show("Hay datos incompletos o incorrectos en la configuración.\nPor favor, verifiquela antes de continuar...",
                                "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool camposVacios =
                String.IsNullOrWhiteSpace(textBox1.Text) &&
                String.IsNullOrWhiteSpace(textBox2.Text) &&
                string.IsNullOrWhiteSpace(textBox3.Text);
            if (camposVacios) {
                MessageBox.Show("No se ha completado ninguno de los campos de búsqueda,\npor favor cargue algún dato primero.",
                                "Carga", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (backgroundWorker.IsBusy) {
                DialogResult abortTransaction = MessageBox.Show("No se ha completado la transacción anterior...\nPor favor espere.",
                                                                "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                //if (abortTransaction == DialogResult.Yes) {
                //    backgroundWorker = new BackgroundWorker();
                //} else {
                //    return;
                //}
                return;
            }
            if (connectionOk == true) {
                toolStripProgressBar1.Visible = true;
                label1.Text = "";
                count = 0;
                //try {
                if (radioButton1.Checked) {
                    bgwObj.id = int.Parse(textBox1.Text);
                    bgwObj.actionIs = backgroundObject.action.searchByID;
                    backgroundWorker.RunWorkerAsync(bgwObj);
                }
                if (radioButton2.Checked) {
                    bgwObj.accessionNumber = int.Parse(textBox2.Text);
                    bgwObj.actionIs = backgroundObject.action.searchByAccessionNumber;
                    backgroundWorker.RunWorkerAsync(bgwObj);
                }
                if (radioButton3.Checked) {
                    count = db.CountStudiesByName(textBox3.Text);

                    if (count > 100) {
                        DialogResult question =
                            MessageBox.Show(
                                string.Format(
                                    "Con ese nombre existen {0} resultados.\n" +
                                    "Sólo se traeran los primeros {1}\n" +
                                    "La consulta puede demorar...\n" +
                                    "¿Desea continuar de todas formas?",
                                    count, Properties.Settings.Default.limitResultsTo),
                                "Consulta",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                        if (question == DialogResult.No) {
                            toolStripProgressBar1.Visible = false;
                            return;
                        }
                    }

                    bgwObj.name = textBox3.Text;
                    bgwObj.actionIs = backgroundObject.action.searchByName;
                    backgroundWorker.RunWorkerAsync(bgwObj);
                }

                //}
                //catch (MySql.Data.MySqlClient.MySqlException error) {
                //    MySqlErrorHandler(error.Number);
                //}
                //catch (FormatException) {
                //    MessageBox.Show("Se ha ingresado información en un formato incorrecto, por favor corrobore los campos.",
                //        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //}
                //catch (OverflowException) {
                //    MessageBox.Show("Uno de los valores está fuera del rango esperado, por favor corrobore los campos.",
                //        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //}
                //finally {
                //  //  toolStripProgressBar1.Visible = false;
                //}
            } else {
                MessageBox.Show("Disculpe, en este momento hay problemas con la conexión al servidor de la base de datos...\n\n" +
                    "Por favor, reintente más tarde.", "Problema con la conexión al servidor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MySqlConnectionOk() {
            connectionOk = true;
            lblTryingToRecconect.Visible = false;
            lblConnectionStatus.Text = "Conectado correctamente a la base de datos";
            lblConnectionStatus.ForeColor = System.Drawing.Color.Green;
        }

        private void MySqlErrorHandler(int errorNumber) {
            switch (errorNumber) {
                case 0:
                    connectionOk = false;
                    lblTryingToRecconect.Visible = true;
                    lblConnectionStatus.Text = "No hay conexión con la base de datos.";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Red;
                    break;
                case 1045:
                    connectionOk = false;
                    lblTryingToRecconect.Visible = false;
                    lblConnectionStatus.Text = "Usuario y/o contraseñas de la base de datos incorrectos.";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Red;
                    break;
                default:
                    connectionOk = false;
                    lblTryingToRecconect.Visible = true;
                    lblConnectionStatus.Text = "Ocurrió un error no contemplado.";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Red;
                    break;
            }
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e) {
            if (dataGridView1.SelectedRows.Count == 1) {
                if (dataGridView1.SelectedRows[0].Cells["N° Acc."].Value.ToString() == "0") {
                    ProtocoloIncorrecto();
                    return;
                }

                accessionNumber = dataGridView1.SelectedRows[0].Cells["N° Acc."].Value.ToString();
                try {
                    reportPath = AmbulatorioReportPath(reportUNC, accessionNumber);

                    if (File.Exists(reportPath)) {
                        ConTurnoConInforme();
                        return;
                    } else { ConTurnoSinInforme(); }
                }
                catch (Exception) { }
            } else { flowCommands.Controls.Clear(); }
        }

        // I had problems to decide on which type use for accessionNumber.
        // Fact is that our crappy system use INT type, but Accession Number
        // on DICOM standard is actualy STRING.
        public string AmbulatorioReportPath(string UNC, string accessionNumber) {
            string reportFolder = string.Format("{0}-{1}", accessionNumber.Substring(0, 2), accessionNumber.Substring(2, 3));
            string reportPath = Path.Combine(UNC, reportFolder, accessionNumber + ".doc");
            return reportPath;
        }

        private void textBox1_Enter(object sender, EventArgs e) {
            radioButton1.Checked = true;
        }

        private void textBox2_Enter(object sender, EventArgs e) {
            radioButton2.Checked = true;
        }

        private void textBox3_Enter(object sender, EventArgs e) {
            radioButton3.Checked = true;
        }

        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e) {
            if (dataGridView1.Rows.Count == 0 && radioButton2.Checked && !String.IsNullOrWhiteSpace(textBox2.Text)) {
                try {
                    int accessionNumber = int.Parse(textBox2.Text);
                    if (accessionNumber > 0) {
                        reportPath = AmbulatorioReportPath(reportUNC, accessionNumber.ToString());
                        if (File.Exists(reportPath)) {
                            SinTurnoConInforme();
                            return;
                        } else { SinTurnoSinInforme(); }
                    }
                }
                catch (FormatException) {
                    MessageBox.Show("Se ha ingresado información en un formato incorrecto, por favor corrobore los campos.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (OverflowException) {
                    MessageBox.Show("Uno de los valores está fuera del rango esperado, por favor corrobore los campos.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else {
                if (count > 0) {
                    label1.Text = string.Format("Viendo {0} de {1}", dataGridView1.Rows.Count, count);
                }
            }
            toolStripProgressBar1.Visible = false;
        }

        private void ProtocoloIncorrecto() {
            Label label = new Label();
            label.Text = protocoloIncorrecto;
            label.AutoSize = true;
            label.ForeColor = System.Drawing.Color.Red;
            label.Padding = new Padding(0, 8, 0, 0);

            flowCommands.Controls.Clear();
            flowCommands.Controls.Add(label);
        }

        private void ConTurnoConInforme() {
            Label label = new Label();
            label.Text = conTurnoConInforme;
            label.AutoSize = true;
            label.ForeColor = System.Drawing.Color.Green;
            label.Padding = new Padding(0, 8, 0, 0);

            Button button = new Button();
            button.Text = "Abrir";
            button.FlatStyle = FlatStyle.Flat;
            button.AutoSize = true;
            button.Click += AbrirInforme;

            flowCommands.Controls.Clear();
            flowCommands.Controls.Add(label);
            flowCommands.Controls.Add(button);
            flowCommands.Controls.Add(check);
        }
        
        private void ConTurnoSinInforme() {
            Label label = new Label();
            label.Text = conTurnoSinInforme;
            label.AutoSize = true;
            label.ForeColor = System.Drawing.Color.Red;
            label.Padding = new Padding(0, 8, 0, 0);

            flowCommands.Controls.Clear();
            flowCommands.Controls.Add(label);
        }

        private void SinTurnoSinInforme() {
            Label label = new Label();
            label.Text = sinTurnoSinInforme;
            label.AutoSize = true;
            label.ForeColor = System.Drawing.Color.Red;
            label.Padding = new Padding(0, 8, 0, 0);

            flowCommands.Controls.Clear();
            flowCommands.Controls.Add(label);
        }

        private void SinTurnoConInforme() {
            Label label = new Label();
            label.Text = sinTurnoConInforme;
            label.AutoSize = true;
            label.ForeColor = System.Drawing.Color.Red;
            label.Padding = new Padding(0, 8, 0, 0);

            Button button = new Button();
            button.Text = "Abrir";
            button.FlatStyle = FlatStyle.Flat;
            button.Click += AvisoSinTurnoConInforme;

            flowCommands.Controls.Clear();
            flowCommands.Controls.Add(label);
            flowCommands.Controls.Add(button);
            flowCommands.Controls.Add(check);
        }

        private void AvisoSinTurnoConInforme(object sender, EventArgs e) {
            MessageBox.Show("Tené en cuenta de que el ambulatorio puede haber confundido números de protocolo.\n\n" +
                "Por favor, verificá que el cuerpo del informe coincida con la práctica realizada.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            AbrirInforme(this, e);
        }

        private void AbrirInforme(object sender, EventArgs e) {
            Process office = new Process();

            // Standard edition
            office.StartInfo.FileName = "swriter";
            // Customized edition
            // office.StartInfo.FileName = "oowriter";
     
            if(soloLectura) office.StartInfo.Arguments = "-nologo -norestore -view " + reportPath;
            else office.StartInfo.Arguments = "-nologo -norestore " + reportPath;

            office.Start();
        }

        private void button2_Click(object sender, EventArgs e) {
            var config = new Config();
            try {
                errorTimer.Enabled = false;
                config.ShowDialog();
            }
            catch (System.Exception) {
                MessageBox.Show("Algo de lo que escribiste colapsó al conector de MySQL.\n\nCausas del error pueden ser, por ejemplo, \nuna IP con una letra o más de 256 hosts...\n\n" +
                    "Intentá de nuevo, son cosas que pasan.", "Super error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally {
                errorTimer.Enabled = true;
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e) {
            if (((TextBox)sender).Text.Length == 0) {
                ((TextBox)sender).BackColor = System.Drawing.Color.White; return; }
            try {
                int.Parse(textBox2.Text);
                if (((TextBox)sender).Text.Length == 8) {
                    ((TextBox)sender).BackColor = System.Drawing.Color.LightGreen; return; }
                if (((TextBox)sender).Text.Length > 8) {
                    ((TextBox)sender).BackColor = System.Drawing.Color.LightPink; return; }
                else { ((TextBox)sender).BackColor = System.Drawing.Color.Khaki; }
            }
            catch (Exception) { ((TextBox)sender).BackColor = System.Drawing.Color.LightPink; }
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
            if (((TextBox)sender).Text.Length == 0) {
                ((TextBox)sender).BackColor = System.Drawing.Color.White; return;
            }
            try {
                int.Parse(((TextBox)sender).Text);
                ((TextBox)sender).BackColor = System.Drawing.Color.White;

            }
            catch (Exception) { ((TextBox)sender).BackColor = System.Drawing.Color.LightPink; }
        }

        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e) {
                if ((string)e.FormattedValue == "0") {
                    e.CellStyle.BackColor = System.Drawing.Color.LightPink;
                    e.CellStyle.ForeColor = System.Drawing.Color.Red;
                }
        }
    }
}