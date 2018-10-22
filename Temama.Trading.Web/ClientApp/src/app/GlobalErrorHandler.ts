import { ErrorHandler, Injectable, ElementRef, ViewChild} from '@angular/core';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {

  @ViewChild('errorModalMessage') errMsgElement: ElementRef
  @ViewChild('errorModal') errModalElement: ElementRef

  constructor() { }

  handleError(error) {
    console.log('Hio');

    this.showError(error.message ? error.message : error.toString());

    // IMPORTANT: Rethrow the error otherwise it gets swallowed
    throw error;
  }

  showError(msg) {
    this.errMsgElement.nativeElement.text = msg;
    this.errModalElement.nativeElement.addClass = 'fade show';
  }
}
