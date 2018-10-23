import { Component, Inject } from '@angular/core';
import { HttpClient } from "@angular/common/http";

@Component({
  selector: 'app-app-log',
  templateUrl: './app-log.component.html',
  styleUrls: ['./app-log.component.css']
})
/** app-log component*/
export class AppLogComponent {
  private logFile = "Loading...";
  private appLogUrl = "api/AppLog/GetAppLog";
  /** app-log ctor */
  constructor(private http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    http.get(baseUrl + this.appLogUrl).subscribe(result => { this.logFile = result.toString(); }, error => { throw error; });
  }
}
