import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { AnalysesReport } from './analyses-report/analyses-report';
import { DetectorsReport } from './detectors-report/detectors-report';
import { DisagreementsReport } from './disagreements-report/disagreements-report';
import { ImagesReport } from './images-report/images-report';

@Component({
  selector: 'app-reports',
  imports: [AnalysesReport, DetectorsReport, DisagreementsReport, ImagesReport, MatTabsModule],
  templateUrl: './reports.html',
})
export class Reports {}
