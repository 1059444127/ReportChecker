using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.IO;

namespace ReportChecker {
    public partial class Config : Form {
        public Config() {
            InitializeComponent();
        }

        int fieldsAmount = 6;
        Label[] label;
        TextBox[] textBox;

        private void Config_Load(object sender, EventArgs e) {
            label = new Label[fieldsAmount];
            textBox = new TextBox[fieldsAmount];

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(GeneralException);

            for (int fieldNumber = 0; fieldNumber < fieldsAmount; ++fieldNumber) {
                label[fieldNumber] = new Label();
                textBox[fieldNumber] = new TextBox();
                textBox[fieldNumber].Size = new Size(200, 20);

                tableLayoutPanel1.Controls.Add(label[fieldNumber]);
                tableLayoutPanel1.Controls.Add(textBox[fieldNumber]);
            }

            label[0].Text = "Server address ";
            textBox[0].Text = Properties.Settings.Default.serverAddress;

            label[1].Text = "Server port ";
            textBox[1].Text = Properties.Settings.Default.serverPort;

            label[2].Text = "Database ";
            textBox[2].Text = Properties.Settings.Default.databaseName;

            label[3].Text = "User ";
            textBox[3].Text = Properties.Settings.Default.databaseUsername;

            label[4].Text = "Password ";
            textBox[4].UseSystemPasswordChar = true;

            label[5].Text = "Report UNC ";
            textBox[5].Text = Properties.Settings.Default.reportUNC;

            checkBox1.Checked = Properties.Settings.Default.useWildcard;
            checkBox2.Checked = Properties.Settings.Default.limitResults;
            numericUpDown1.Value = Properties.Settings.Default.limitResultsTo;

            textBox[4].Text = SSTCryptographer.Decrypt(
                                                Properties.Settings.Default.databasePassword,
                                                Properties.Settings.Default.databaseUsername +
                                                "as56afs65qer3g654afg");
        }

        private void GeneralException(object sender, UnhandledExceptionEventArgs e) {
            Console.WriteLine("[EXCEPTION] " + ((Exception)e.ExceptionObject).Message);
        }

        private void button2_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e) {
            
            Properties.Settings.Default.serverAddress = textBox[0].Text;
            Properties.Settings.Default.serverPort = textBox[1].Text;
            Properties.Settings.Default.databaseName = textBox[2].Text;
            Properties.Settings.Default.databaseUsername = textBox[3].Text;
            Properties.Settings.Default.databasePassword =
                SSTCryptographer.Encrypt(textBox[4].Text, Properties.Settings.Default.databaseUsername + "as56afs65qer3g654afg");
            Properties.Settings.Default.reportUNC = textBox[5].Text;
            Properties.Settings.Default.useWildcard = checkBox1.Checked;
            Properties.Settings.Default.limitResults = checkBox2.Checked;
            Properties.Settings.Default.limitResultsTo = (int)numericUpDown1.Value;
            Properties.Settings.Default.Save();
            Application.Restart();
        }

        private void button3_Click(object sender, EventArgs e) {
            dbMySql.dbMySql db = new dbMySql.dbMySql();
            db.serverAddress = textBox[0].Text;
            db.serverPort = textBox[1].Text;
            db.databaseName = textBox[2].Text;
            db.databaseUsername = textBox[3].Text;
            db.databasePassword = textBox[4].Text;

            progressBar1.Visible = true;
            button3.Enabled = false;

            var bgw = new BackgroundWorker();
            bgw.DoWork += (bgwSender, bgwE) => {
                ((dbMySql.dbMySql)bgwE.Argument).CreateConnection();
                ((dbMySql.dbMySql)bgwE.Argument).TestConnection();
            };
            bgw.RunWorkerCompleted += dbResult;
            bgw.RunWorkerAsync(db);
        }

        private void dbResult(object sender, RunWorkerCompletedEventArgs e) {
            progressBar1.Visible = false;
            button3.Enabled = true;
            if (e.Error == null) {
                label1.Text = "OK!";
                label1.ForeColor = System.Drawing.Color.Green;
            } else {
                if (e.Error is InvalidCastException) {
                    label1.Text = "Error de carga";
                    return;
                } else if (e.Error is MySqlException) {
                    label1.ForeColor = System.Drawing.Color.Red;
                    int errorNumber = ((MySqlException)e.Error).Number;
                    switch (errorNumber) {
                        case 1042: // ER_BAD_HOST
                            label1.Text = "Sin conexión";
                            break;
                        case 1043: // ER_HANDSHAKE
                        case 1044: // ER_DBACCESS_DENIED
                        case 1045: // ER_ACCESS_DENIED
                            label1.Text = "Acceso negado";
                            break;
                        case 1046: // ER_NO_DB
                        case 1049: // ER_BAD_DB
                            label1.Text = "Error nombre db";
                            break;
                        default:
                            label1.Text = "Error N " + errorNumber.ToString();
                            break;
                    }
                } else if (e.Error is ArgumentException || e.Error is InvalidCastException) {
                    label1.Text = "Error de carga";
                } else if (e.Error is NullReferenceException) {
                    label1.Text = "Error programación";
                }
            }
        }

        private void button4_Click(object sender, EventArgs e) {
            try {
                if (Directory.Exists(textBox[5].Text)) {
                    label3.Text = "OK!";
                    label3.ForeColor = System.Drawing.Color.Green;
                } else {
                    label3.Text = "No existe";
                    label3.ForeColor = System.Drawing.Color.Red;
                }
            }
            catch (Exception) {
                label3.Text = "Error...";
                label3.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e) {
            if (numericUpDown1.Value > 1000) {
                MessageBox.Show("¡Cuidado! Las consultas podrían tardar demasiado tiempo en terminar o,\n" +
                    "incluso, no completarse nunca.",
                    "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            if (numericUpDown1.Value < 1) {
                checkBox2.Checked = false;
                numericUpDown1.Value = 1;
            }
        }
    }
}
