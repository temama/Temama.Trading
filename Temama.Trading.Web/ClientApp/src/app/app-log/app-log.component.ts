import { Component } from '@angular/core';
import { HttpClient } from "@angular/common/http";

@Component({
  selector: 'app-app-log',
  templateUrl: './app-log.component.html',
  styleUrls: ['./app-log.component.css']
})
/** app-log component*/
export class AppLogComponent {
  private logFile = "Loading...";
  private appLogUrl = "";
  /** app-log ctor */
  constructor(private http: HttpClient) { }

  ngOnInit() {
    //this.logFile = this.http.get(this.appLogUrl);
  }
}
