import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule],
  templateUrl: './confirm-dialog.html',
})
export class ConfirmDialog {
  message = inject<string>(MAT_DIALOG_DATA);
}
