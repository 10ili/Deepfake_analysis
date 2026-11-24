import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-rename-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './rename-dialog.html',
  styleUrl: './rename-dialog.scss',
})
export class RenameDialog {
  private dialogRef = inject(MatDialogRef<RenameDialog>);
  private fileName = inject<string>(MAT_DIALOG_DATA);
  private dot = this.fileName.lastIndexOf('.');

  extension = this.fileName.slice(this.dot);

  name = new FormControl(this.fileName.slice(0, this.dot), {
    nonNullable: true,
    validators: [
      Validators.required,
      Validators.maxLength(255 - this.extension.length),
      Validators.pattern(/^(?=.*\S)[^\\/:*?"<>|]+$/),
    ],
  });

  save() {
    if (this.name.valid) {
      this.dialogRef.close(this.name.value.trim() + this.extension);
    }
  }
}
